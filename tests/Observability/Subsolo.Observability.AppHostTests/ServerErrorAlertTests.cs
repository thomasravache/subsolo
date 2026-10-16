using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Subsolo.Observability.AppHostTests;

/// <summary>
/// SPEC-0009 IT-06 — no ambiente local do AppHost (Seq com <c>infra/seq/alerts.json</c> aplicado e Mailpit como
/// servidor de e-mail), 6 respostas 500 da API em menos de 5 minutos (PostgreSQL parado, agenda pedida 6 vezes)
/// disparam o alerta "Erros 5xx": um e-mail chega ao Mailpit com o link do runbook <c>docs/runbooks/erros-5xx.md</c>.
/// </summary>
public sealed class ServerErrorAlertTests
{
    private const string Runbook = "docs/runbooks/erros-5xx.md";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Janela do alerta (5 min) mais a folga da avaliação periódica do Seq.</summary>
    private static readonly TimeSpan AlertTimeout = TimeSpan.FromMinutes(7);

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task SixServerErrorsInFiveMinutesSendAnAlertEmailWithTheRunbook()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.StartAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("seq", ct).WaitAsync(StartupTimeout, ct);

        using var mailpit = new HttpClient { BaseAddress = app.GetEndpoint("mailpit", "http") };
        (await mailpit.DeleteAsync(new Uri("/api/v1/messages", UriKind.Relative), ct)).EnsureSuccessStatusCode();

        var stop = await app.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StopCommand, ct);
        stop.Success.ShouldBeTrue($"não foi possível parar o PostgreSQL: {stop.Message}");
        await app.ResourceNotifications.WaitForResourceAsync("postgres", KnownResourceStates.Exited, ct)
            .WaitAsync(StartupTimeout, ct);

        using var api = app.CreateHttpClient("api");
        var statuses = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            using var response = await api.GetAsync(new Uri("/api/v1/cities/cidade-exemplo/agenda", UriKind.Relative), ct);
            statuses.Add((int)response.StatusCode);
        }

        statuses.ShouldAllBe(status => status == 500, $"pré-condição: a API não respondeu 500 com o banco parado ({string.Join(", ", statuses)})");

        var alert = await WaitForAlertAsync(mailpit, ct);
        alert.ShouldNotBeNull($"nenhum e-mail com o runbook {Runbook} chegou ao Mailpit em {AlertTimeout.TotalMinutes} min");
    }

    private static async Task<MessageDetail?> WaitForAlertAsync(HttpClient mailpit, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + AlertTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var list = await mailpit.GetFromJsonAsync<MessageList>("/api/v1/messages", ct) ?? new MessageList([]);
            foreach (var summary in list.Messages)
            {
                var detail = await mailpit.GetFromJsonAsync<MessageDetail>($"/api/v1/message/{Uri.EscapeDataString(summary.Id)}", ct);
                if (detail is not null &&
                    ((detail.Text ?? string.Empty).Contains(Runbook, StringComparison.Ordinal) ||
                     (detail.Html ?? string.Empty).Contains(Runbook, StringComparison.Ordinal)))
                {
                    return detail;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        return null;
    }

    private sealed record MessageList([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary([property: JsonPropertyName("ID")] string Id);

    private sealed record MessageDetail(
        [property: JsonPropertyName("Subject")] string? Subject,
        [property: JsonPropertyName("Text")] string? Text,
        [property: JsonPropertyName("HTML")] string? Html);
}
