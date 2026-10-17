using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0010 IT-03 — rate limiting por IP (<c>Microsoft.AspNetCore.RateLimiting</c>): na política <c>auth</c>
/// (5/min), a 6ª chamada do mesmo IP em 1 minuto responde 429 ProblemDetails <c>code=security.rate_limited</c> com
/// <c>Retry-After</c>, enquanto outro IP continua passando; o mesmo vale para <c>report</c> (10/h) e
/// <c>default</c> (100/min).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RateLimitingTests(PostgresFixture postgres)
{
    [Theory]
    [Trait("Spec", "SPEC-0010:IT-03")]
    [Trait("Kind", "Integration")]
    [InlineData("auth", 5)]
    [InlineData("report", 10)]
    [InlineData("default", 100)]
    public async Task CallAboveThePolicyLimitFromTheSameIpReturns429WithRetryAfter(string policy, int limit)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var route = SecurityTestEndpoints.RateLimitedPrefix + policy;

        for (var call = 1; call <= limit; call++)
        {
            using var allowed = await client.GetAsync(route, ct);
            allowed.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"chamada {call} de {limit} na política {policy}: {await allowed.Content.ReadAsStringAsync(ct)}");
        }

        using var limited = await client.GetAsync(route, ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        limited.Headers.RetryAfter.ShouldNotBeNull("429 sem Retry-After");
        (limited.Headers.RetryAfter.Delta ?? (limited.Headers.RetryAfter.Date - DateTimeOffset.UtcNow))
            .ShouldNotBeNull()
            .ShouldBeGreaterThan(TimeSpan.Zero);

        using var otherIp = new HttpRequestMessage(HttpMethod.Get, route);
        otherIp.Headers.Add(SecurityApiFactory.RemoteIpHeader, "198.51.100.20");
        using var fromOtherIp = await client.SendAsync(otherIp, ct);
        fromOtherIp.StatusCode.ShouldBe(HttpStatusCode.OK, "o limite é por IP: outro IP não pode ser bloqueado");
    }
}
