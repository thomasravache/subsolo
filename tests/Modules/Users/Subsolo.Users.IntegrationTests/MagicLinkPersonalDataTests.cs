using System.Diagnostics;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-05 (ADR-0008) — com os logs e spans exportados capturados em memória durante o pedido e o consumo do
/// link, nenhum deles contém o e-mail em claro (só mascarado) nem o token do link.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkPersonalDataTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task RequestAndConsumeDoNotExportTheEmailOrTheToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        var email = MagicLinkHost.NewEmail("fa.sigiloso");

        var token = await client.RequestTokenAsync(mailpit, email, ct);
        using (var consumed = await client.ConsumeMagicLinkAsync(token, ct))
        {
            consumed.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await consumed.Content.ReadAsStringAsync(ct)));
        }

        var spans = await SpansWhenAsync(api, IsServerSpanOf(MagicLinkHost.ConsumeRoute), ct);
        spans.ShouldContain(span => IsServerSpanOf(MagicLinkHost.RequestRoute)(span), "o span do pedido não foi exportado");
        spans.ShouldContain(span => IsServerSpanOf(MagicLinkHost.ConsumeRoute)(span), "o span do consumo não foi exportado");
        var logs = api.Logs;
        logs.ShouldNotBeEmpty("nenhum log exportado");

        var signals = logs.SelectMany(log =>
                new[] { log.FormattedMessage, log.Body, log.Exception?.ToString() }
                    .Concat((log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}"))
                    .Select(text => ($"log {log.CategoryName}", text)))
            .Concat(spans.SelectMany(span =>
                new[] { span.DisplayName, span.StatusDescription }
                    .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}")))
                    .Select(text => ($"span {span.DisplayName}", text))))
            .Where(item => item.text is not null)
            .ToList();

        Leaks(signals, email, StringComparison.OrdinalIgnoreCase).ShouldBeEmpty("e-mail em claro nos sinais exportados");
        Leaks(signals, token, StringComparison.Ordinal).ShouldBeEmpty("token do link nos sinais exportados");
    }

    private static List<string> Leaks(IEnumerable<(string Where, string? Text)> signals, string secret, StringComparison comparison) =>
        [.. signals
            .Where(item => item.Text!.Contains(secret, comparison))
            .Select(item => $"{item.Where}: {item.Text}")
            .Distinct(StringComparer.Ordinal)];

    private static async Task<IReadOnlyList<Activity>> SpansWhenAsync(
        UsersApiFactory api, Func<Activity, bool> predicate, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var spans = api.Spans;
            if (spans.Any(predicate) || watch.Elapsed >= SpanTimeout)
            {
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }
    }

    private static Func<Activity, bool> IsServerSpanOf(string route) =>
        span => span.Kind == ActivityKind.Server &&
                span.TagObjects.Any(tag => tag.Value?.ToString()?.EndsWith(route, StringComparison.Ordinal) == true);
}
