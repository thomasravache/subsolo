using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// Mailpit real num container efêmero (a imagem fixa do <see cref="MailpitFixture"/>), com a leitura completa de cada
/// mensagem pela API HTTP do Mailpit: remetente com nome, assunto, partes HTML e texto e o código-fonte (para conferir
/// as partes MIME). O <see cref="MailpitFixture"/> devolve só o texto e o endereço do remetente.
/// </summary>
public sealed class MailpitServer : IAsyncLifetime
{
    private const int SmtpContainerPort = 1025;
    private const int HttpContainerPort = 8025;
    private const int PageSize = 500;

    private static readonly TimeSpan ArrivalTimeout = TimeSpan.FromSeconds(15);

    private IContainer? _container;
    private HttpClient? _api;

    public string SmtpHost => Container.Hostname;

    public int SmtpPort => Container.GetMappedPublicPort(SmtpContainerPort);

    private IContainer Container => _container
        ?? throw new InvalidOperationException("MailpitServer ainda não inicializado (InitializeAsync).");

    private HttpClient Api => _api
        ?? throw new InvalidOperationException("MailpitServer ainda não inicializado (InitializeAsync).");

    /// <summary>
    /// As mensagens endereçadas a <paramref name="to"/>, esperando até <paramref name="count"/> chegarem (ou o prazo
    /// de 15 s passar; o teste confere a contagem).
    /// </summary>
    public async Task<IReadOnlyList<MailpitMail>> WaitForMessagesAsync(string to, int count, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ArrivalTimeout);
        var summaries = await SummariesAsync(to, cancellationToken);
        while (summaries.Count < count && !timeout.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            summaries = await SummariesAsync(to, cancellationToken);
        }

        var messages = new List<MailpitMail>();
        foreach (var summary in summaries)
        {
            var id = Uri.EscapeDataString(summary.Id);
            var detail = await Api.GetFromJsonAsync<MessageDetail>($"api/v1/message/{id}", cancellationToken)
                ?? throw new InvalidOperationException($"mensagem {summary.Id} sumiu do Mailpit");
            var raw = await Api.GetStringAsync(new Uri($"api/v1/message/{id}/raw", UriKind.Relative), cancellationToken);
            messages.Add(new MailpitMail(
                summary.Id,
                detail.From?.Name ?? string.Empty,
                detail.From?.Address ?? string.Empty,
                [.. (detail.To ?? []).Select(address => address.Address)],
                detail.Subject ?? string.Empty,
                detail.Html ?? string.Empty,
                detail.Text ?? string.Empty,
                raw));
        }

        return messages;
    }

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder(MailpitFixture.Image)
            .WithPortBinding(SmtpContainerPort, assignRandomHostPort: true)
            .WithPortBinding(HttpContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(HttpContainerPort).ForPath("/api/v1/info")))
            .Build();
        await _container.StartAsync();

        _api = new HttpClient
        {
            BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpContainerPort)}/"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        _api?.Dispose();
        _api = null;
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    private async Task<List<MessageSummary>> SummariesAsync(string to, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"to:\"{to}\"");
        var search = await Api.GetFromJsonAsync<SearchResponse>($"api/v1/search?query={query}&limit={PageSize}", cancellationToken)
            ?? new SearchResponse([]);
        return [.. search.Messages.Where(m => m.To.Any(a => string.Equals(a.Address, to, StringComparison.OrdinalIgnoreCase)))];
    }

    private sealed record SearchResponse([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("To")] IReadOnlyList<MailAddress> To);

    private sealed record MessageDetail(
        [property: JsonPropertyName("From")] MailAddress? From,
        [property: JsonPropertyName("To")] IReadOnlyList<MailAddress>? To,
        [property: JsonPropertyName("Subject")] string? Subject,
        [property: JsonPropertyName("HTML")] string? Html,
        [property: JsonPropertyName("Text")] string? Text);

    private sealed record MailAddress(
        [property: JsonPropertyName("Name")] string? Name,
        [property: JsonPropertyName("Address")] string Address);
}

/// <summary>Mensagem lida do Mailpit: remetente, destinatários, assunto, partes HTML e texto e o código-fonte MIME.</summary>
public sealed record MailpitMail(
    string Id,
    string FromName,
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    string Html,
    string Text,
    string Raw);

/// <summary>
/// Um Mailpit para todas as classes de teste que mandam e-mail de verdade. SPEC-0030: também o PostgreSQL da API em
/// memória (<see cref="PostgresFixture"/>), na mesma coleção, para a máquina de teste subir um container de cada.
/// </summary>
[CollectionDefinition(nameof(MailpitCollection))]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Convenção de coleção do xUnit, como a PostgresCollection.")]
public sealed class MailpitCollection : ICollectionFixture<MailpitServer>, ICollectionFixture<PostgresFixture>;
