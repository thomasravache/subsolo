using System.Diagnostics;
using System.Text.RegularExpressions;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-06 (ADR-0008) — com os logs e spans exportados capturados em memória durante o fluxo completo do Google
/// (início, authorize, retorno e <c>/me</c>), nenhum deles contém o e-mail em claro (só mascarado), o <c>id_token</c>
/// (nenhum JWT), o <c>code</c> nem o <c>state</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed partial class GooglePersonalDataTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task GoogleFlowDoesNotExportTheEmailTheIdTokenTheCodeOrTheState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        var flow = await client.SignInWithGoogleAsync(google, FakeGoogleProvider.Issuers.Verified, "/curitiba", ct);
        using (var callback = flow.Callback)
        {
            callback.RedirectTarget().ShouldBe("/curitiba", "o fluxo completo precisa terminar com a entrada");
        }

        using (var me = await client.GetAsync("/api/v1/me", ct))
        {
            me.IsSuccessStatusCode.ShouldBeTrue(UsersApiClient.Excerpt(await me.Content.ReadAsStringAsync(ct)));
        }

        var spans = await SpansWhenAsync(api, IsServerSpanOf(GoogleHost.CallbackRoute), ct);
        spans.ShouldContain(span => IsServerSpanOf(GoogleHost.StartRoute)(span), "o span do início não foi exportado");
        spans.ShouldContain(span => IsServerSpanOf(GoogleHost.CallbackRoute)(span), "o span do retorno não foi exportado");
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

        var code = flow.FromProvider.Parameters["code"];
        var state = flow.FromProvider.Parameters["state"];
        Leaks(signals, FakeGoogleProvider.VerifiedEmail, StringComparison.OrdinalIgnoreCase).ShouldBeEmpty("e-mail em claro nos sinais exportados");
        Leaks(signals, code, StringComparison.Ordinal).ShouldBeEmpty("code do provedor nos sinais exportados");
        Leaks(signals, state, StringComparison.Ordinal).ShouldBeEmpty("state nos sinais exportados");
        signals.Where(item => Jwt().IsMatch(item.text!))
            .Select(item => $"{item.Item1}: {item.text}")
            .Distinct(StringComparer.Ordinal)
            .ShouldBeEmpty("id_token (JWT) nos sinais exportados");
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

    /// <summary>Um JWT (cabeçalho e corpo em base64url de JSON: <c>eyJ…</c>.<c>eyJ…</c>.).</summary>
    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]{8,}\.eyJ[A-Za-z0-9_\-]{8,}\.", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Jwt();
}
