using System.Diagnostics;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-07 — com o SMTP parado (porta sem serviço), <c>POST /api/v1/auth/magic-link</c> responde 503
/// <c>notifications.email_unavailable</c> em até 11 s (10 s de timeout do envio mais 1 s de folga).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkSmtpUnavailableTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    [Fact]
    [Trait("Spec", "SPEC-0016:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task StoppedSmtpReturns503EmailUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, smtpPort: MagicLinkHost.ClosedPort());
        await using var __ = api;
        using var client = api.CreateUsersClient();
        var csrf = await client.CsrfTokenAsync(ct);

        var watch = Stopwatch.StartNew();
        using var response = await client.SendAsync(
            HttpMethod.Post,
            MagicLinkHost.RequestRoute,
            ct,
            System.Net.Http.Json.JsonContent.Create(new { email = MagicLinkHost.NewEmail("smtp-parado"), returnTo = (string?)null }),
            csrf);
        watch.Stop();

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "notifications.email_unavailable", ct);
        watch.Elapsed.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(11), $"a resposta levou {watch.Elapsed.TotalSeconds:F1} s");
    }
}
