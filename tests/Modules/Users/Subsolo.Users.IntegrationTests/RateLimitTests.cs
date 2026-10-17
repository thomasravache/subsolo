using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-10 (parte HTTP) — com a política <c>default</c> configurada para 3 por minuto, a 4ª chamada do mesmo
/// IP a <c>GET /api/v1/me</c> e a <c>GET /api/v1/cities</c> recebe 429 ProblemDetails <c>code=security.rate_limited</c>
/// com <c>Retry-After</c>. A regra de arquitetura (todo endpoint declara uma política) fica em
/// <c>Subsolo.ArchitectureTests/Security</c> e no host em <c>Subsolo.Security.IntegrationTests</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RateLimitTests(PostgresFixture postgres)
{
    private const int Limit = 3;

    /// <summary>IP só das chamadas contadas (a entrada e o token anti-forgery saem do IP padrão).</summary>
    private const string CountedIp = "198.51.100.30";

    /// <summary>Endereço da rede privada (rede do Compose) de onde o servidor do site chama a API.</summary>
    private const string WebServerIp = "10.0.0.5";

    private const string CitiesRoute = "/api/v1/cities";

    private static readonly Dictionary<string, string> DefaultAtThreePerMinute = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Security:RateLimiting:default:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    public async Task FourthCallToMeReturns429WithRetryAfter()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, settings: DefaultAtThreePerMinute);
        using var client = api.CreateUsersClient();
        await client.SignInAsync("fulano@subsolo.test", ct);
        client.RemoteIp = CountedIp;

        await ShouldLimitTheFourthCallAsync(client, MeEndpointTests.MeRoute, ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    public async Task FourthCallToCitiesReturns429WithRetryAfter()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, settings: DefaultAtThreePerMinute);
        using var client = api.CreateUsersClient();
        client.RemoteIp = CountedIp;

        await ShouldLimitTheFourthCallAsync(client, "/api/v1/cities", ct);
    }

    /// <summary>
    /// SPEC-0014 IT-11 (Emenda v3) — com as chamadas chegando de um endereço da rede privada (o servidor do site, na rede
    /// do Compose) e o IP de cada visitante em <c>X-Forwarded-For</c>, há um balde por visitante: 3 chamadas de cada um
    /// a <c>GET /api/v1/cities</c> passam, e a 4ª do mesmo visitante recebe 429.
    /// </summary>
    [Fact]
    [Trait("Spec", "SPEC-0014:IT-11")]
    [Trait("Kind", "Integration")]
    public async Task EachVisitorBehindTheWebServerHasItsOwnBucket()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, settings: DefaultAtThreePerMinute);
        using var client = api.CreateUsersClient();
        client.RemoteIp = WebServerIp;

        foreach (var visitor in new[] { "203.0.113.7", "198.51.100.44" })
        {
            client.ForwardedFor = visitor;
            for (var call = 1; call <= Limit; call++)
            {
                using var allowed = await client.GetAsync(CitiesRoute, ct);
                allowed.StatusCode.ShouldBe(
                    HttpStatusCode.OK,
                    $"visitante {visitor}, chamada {call} de {Limit}: o limite deve ser por visitante, não pelo servidor do site — " +
                    UsersApiClient.Excerpt(await allowed.Content.ReadAsStringAsync(ct)));
            }
        }

        client.ForwardedFor = "203.0.113.7";
        using var limited = await client.GetAsync(CitiesRoute, ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
    }

    private static async Task ShouldLimitTheFourthCallAsync(UsersApiClient client, string route, CancellationToken ct)
    {
        for (var call = 1; call <= Limit; call++)
        {
            using var allowed = await client.GetAsync(route, ct);
            allowed.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"chamada {call} de {Limit} a {route}: {UsersApiClient.Excerpt(await allowed.Content.ReadAsStringAsync(ct))}");
        }

        using var limited = await client.GetAsync(route, ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        limited.Headers.RetryAfter.ShouldNotBeNull($"429 de {route} sem Retry-After");
        (limited.Headers.RetryAfter.Delta ?? (limited.Headers.RetryAfter.Date - DateTimeOffset.UtcNow))
            .ShouldNotBeNull()
            .ShouldBeGreaterThan(TimeSpan.Zero);
    }
}
