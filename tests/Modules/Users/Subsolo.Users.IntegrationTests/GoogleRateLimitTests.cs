using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-05 — com o limite <c>auth</c> em 5 por minuto, o mesmo IP chama <c>GET /api/v1/auth/google</c> 6 vezes:
/// as 5 primeiras vão para o provedor (302) e a 6ª recebe 429 <c>security.rate_limited</c> com <c>Retry-After</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleRateLimitTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    private static readonly Dictionary<string, string> AuthAtFivePerMinute = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:auth:PermitLimit"] = "5",
        ["Security:RateLimiting:auth:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task SixthStartFromTheSameIpIsRateLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = GoogleHost.Merge(GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), AuthAtFivePerMinute);
        var (api, _) = await GoogleHost.StartAsync(postgres, mailpit, settings, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        client.RemoteIp = "198.51.100.71";

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var allowed = await client.StartGoogleAsync("/curitiba", ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Redirect, $"início {attempt} dentro do limite");
        }

        using var limited = await client.StartGoogleAsync("/curitiba", ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        limited.Headers.RetryAfter.ShouldNotBeNull("429 sem Retry-After");
        (limited.Headers.RetryAfter.Delta ?? (limited.Headers.RetryAfter.Date - DateTimeOffset.UtcNow))
            .ShouldNotBeNull()
            .ShouldBeGreaterThan(TimeSpan.Zero);
    }
}
