using System.Net;
using Microsoft.Extensions.Time.Testing;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-04 — com o <see cref="TimeProvider"/> do host trocado por um <see cref="FakeTimeProvider"/>: 2 h depois
/// da entrada, <c>/me</c> renova <c>expires_at</c> uma vez (para agora + 30 dias); usando a sessão a cada 20 dias,
/// 90 dias e 1 minuto depois da entrada <c>/me</c> volta 401; 200 chamadas seguidas têm a validação da sessão com
/// p95 ≤ 5 ms e não gravam de novo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SessionRenewalTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Sliding = TimeSpan.FromDays(30);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(90);
    private static readonly TimeSpan Precision = TimeSpan.FromSeconds(1);

    /// <summary>As 200 chamadas do IT-04 passam do limite padrão (100/min): o teste sobe o limite.</summary>
    private static readonly Dictionary<string, string> HighRateLimit = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "10000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task UseTwoHoursLaterRenewsOnceAndLaterCallsDoNotWriteAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = Now();
        var clock = new FakeTimeProvider(start);
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, clock: clock, settings: HighRateLimit);
        using var client = api.CreateUsersClient();
        await client.SignInAsync("renova@subsolo.test", ct);

        var initial = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        ShouldBeAbout(initial.ExpiresAtUtc, start + Sliding, "expires_at da entrada");
        ShouldBeAbout(initial.AbsoluteExpiresAtUtc, start + Absolute, "absolute_expires_at da entrada");

        clock.Advance(TimeSpan.FromHours(2));
        await MeShouldBeOkAsync(client, ct);
        await MeShouldBeOkAsync(client, ct);

        var renewed = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        ShouldBeAbout(renewed.ExpiresAtUtc, clock.GetUtcNow() + Sliding, "expires_at renovado 2 h depois");
        ShouldBeAbout(renewed.LastSeenAtUtc, clock.GetUtcNow(), "last_seen_at da renovação");
        ShouldBeAbout(renewed.AbsoluteExpiresAtUtc, start + Absolute, "o teto não se move");

        api.Timings.Reset();
        for (var call = 0; call < 200; call++)
        {
            await MeShouldBeOkAsync(client, ct);
        }

        var timings = api.Timings.Durations;
        timings.Count.ShouldBeGreaterThanOrEqualTo(200, "o host não validou a sessão pelo ISessionValidator em cada requisição");
        api.Timings.P95().ShouldBeLessThanOrEqualTo(
            TimeSpan.FromMilliseconds(5),
            $"p95 da validação da sessão em 200 chamadas: {api.Timings.P95().TotalMilliseconds:F2} ms");

        var after = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        after.ExpiresAtUtc.ShouldBe(renewed.ExpiresAtUtc, "chamadas na mesma hora não renovam de novo");
        after.LastSeenAtUtc.ShouldBe(renewed.LastSeenAtUtc, "chamadas na mesma hora não gravam de novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task SessionUsedEvery20DaysEndsNinetyDaysAfterTheSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = Now();
        var clock = new FakeTimeProvider(start);
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, clock: clock, settings: HighRateLimit);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync("teto@subsolo.test", ct);

        foreach (var day in new[] { 20, 40, 60, 80 })
        {
            clock.SetUtcNow(start + TimeSpan.FromDays(day));
            await MeShouldBeOkAsync(client, ct);
            var session = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
            session.ExpiresAtUtc.ShouldBeLessThanOrEqualTo(
                session.AbsoluteExpiresAtUtc, $"no dia {day}, a renovação passou do teto de 90 dias");
        }

        clock.SetUtcNow(start + Absolute + TimeSpan.FromMinutes(1));

        using var response = await client.GetAsync(MeEndpointTests.MeRoute, ct, sessionCookie: client.SessionCookie ?? signedIn.SessionCookie);
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    private static async Task MeShouldBeOkAsync(UsersApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(MeEndpointTests.MeRoute, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct)));
    }

    private static void ShouldBeAbout(DateTimeOffset actual, DateTimeOffset expected, string what) =>
        (actual - expected).Duration().ShouldBeLessThanOrEqualTo(Precision, $"{what}: {actual:O}, esperado {expected:O}");

    /// <summary>Agora, em segundos inteiros (o relógio falso começa perto do real: cookies e chaves seguem coerentes).</summary>
    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }
}
