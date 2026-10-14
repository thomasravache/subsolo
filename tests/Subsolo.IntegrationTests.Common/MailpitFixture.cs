using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — servidor SMTP de teste (Mailpit) num container efêmero. O código sob teste envia para
/// <see cref="SmtpHost"/>:<see cref="SmtpPort"/>; o teste lê as mensagens recebidas pela API do Mailpit.
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    /// <summary>Imagem com tag fixa (ADR-0009).</summary>
    public const string Image = "axllent/mailpit:v1.31.3";

    private const int SmtpContainerPort = 1025;
    private const int HttpContainerPort = 8025;
    private const int PageSize = 500;

    private IContainer? _container;
    private HttpClient? _api;

    public string SmtpHost => Container.Hostname;

    public int SmtpPort => Container.GetMappedPublicPort(SmtpContainerPort);

    private IContainer Container => _container
        ?? throw new InvalidOperationException("MailpitFixture ainda não inicializado (InitializeAsync).");

    private HttpClient Api => _api
        ?? throw new InvalidOperationException("MailpitFixture ainda não inicializado (InitializeAsync).");

    /// <summary>Mensagens recebidas pelo Mailpit endereçadas a <paramref name="to"/>.</summary>
    public async Task<IReadOnlyList<MailpitMessage>> GetMessagesAsync(string to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var query = Uri.EscapeDataString($"to:\"{to}\"");
        var search = await Api.GetFromJsonAsync<SearchResponse>($"api/v1/search?query={query}&limit={PageSize}")
            ?? new SearchResponse([]);

        var messages = new List<MailpitMessage>();
        foreach (var summary in search.Messages.Where(m => m.To.Any(a => string.Equals(a.Address, to, StringComparison.OrdinalIgnoreCase))))
        {
            var detail = await Api.GetFromJsonAsync<MessageResponse>($"api/v1/message/{Uri.EscapeDataString(summary.Id)}");
            messages.Add(new MailpitMessage(
                summary.Id,
                summary.From?.Address ?? string.Empty,
                [.. summary.To.Select(a => a.Address)],
                summary.Subject,
                detail?.Text ?? string.Empty));
        }

        return messages;
    }

    /// <summary>Apaga todas as mensagens recebidas.</summary>
    public async Task ClearAsync()
    {
        using var response = await Api.DeleteAsync(new Uri("api/v1/messages", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask InitializeAsync()
    {
        _container = await DockerEnvironment.StartAsync(() => new ContainerBuilder(Image)
            .WithPortBinding(SmtpContainerPort, assignRandomHostPort: true)
            .WithPortBinding(HttpContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(HttpContainerPort).ForPath("/api/v1/info")))
            .Build());

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

    private sealed record SearchResponse([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("From")] MailAddress? From,
        [property: JsonPropertyName("To")] IReadOnlyList<MailAddress> To,
        [property: JsonPropertyName("Subject")] string Subject);

    private sealed record MailAddress([property: JsonPropertyName("Address")] string Address);

    private sealed record MessageResponse([property: JsonPropertyName("Text")] string? Text);
}
