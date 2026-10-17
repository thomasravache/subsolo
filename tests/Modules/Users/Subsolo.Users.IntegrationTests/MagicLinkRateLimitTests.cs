using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-04 — dados 4 pedidos de link para o mesmo e-mail, os 4 respondem 202 iguais e o Mailpit recebe só 3
/// (limite por e-mail, silencioso); com o limite de IP <c>auth</c> em 5 por minuto, o 6º pedido do mesmo IP (com
/// e-mails diferentes) recebe 429 <c>security.rate_limited</c> com <c>Retry-After</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkRateLimitTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    private static readonly Dictionary<string, string> AuthAtFivePerMinute = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:auth:PermitLimit"] = "5",
        ["Security:RateLimiting:auth:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task FourthRequestForTheSameEmailIsAcceptedButNotSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        var email = MagicLinkHost.NewEmail("limite-email");

        for (var request = 1; request <= 4; request++)
        {
            using var response = await client.RequestMagicLinkAsync(email, ct);
            await response.ShouldBeAcceptedAsync(ct);
        }

        var messages = await MagicLinkHost.WaitForMessagesAsync(mailpit, email, 4, ct, TimeSpan.FromSeconds(3));
        messages.Count.ShouldBe(3, "acima de 3 links em 15 min por e-mail, o pedido não envia");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task SixthRequestFromTheSameIpIsRateLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, overrides: AuthAtFivePerMinute);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        client.RemoteIp = "198.51.100.61";

        for (var request = 1; request <= 5; request++)
        {
            using var allowed = await client.RequestMagicLinkAsync(MagicLinkHost.NewEmail($"ip-{request}"), ct);
            await allowed.ShouldBeAcceptedAsync(ct);
        }

        using var limited = await client.RequestMagicLinkAsync(MagicLinkHost.NewEmail("ip-6"), ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        limited.Headers.RetryAfter.ShouldNotBeNull("429 sem Retry-After");
        (limited.Headers.RetryAfter.Delta ?? (limited.Headers.RetryAfter.Date - DateTimeOffset.UtcNow))
            .ShouldNotBeNull()
            .ShouldBeGreaterThan(TimeSpan.Zero);
    }
}
