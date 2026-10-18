using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-02 — com o link lido do Mailpit, <c>POST /api/v1/auth/magic-link/consume</c> responde 200 com o
/// <c>returnTo</c> pedido e <c>Set-Cookie __Host-subsolo</c>, e <c>/me</c> responde 200; o segundo consumo dá 400
/// <c>users.magic_link_invalid</c>; com o relógio 16 min à frente, um token novo dá 400; token aleatório dá 400; o p95
/// de 20 consumos fica ≤ 300 ms. IT-03 — dois consumos simultâneos do mesmo token: exatamente um 200 e um 400.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkConsumeTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    private const string Invalid = "users.magic_link_invalid";

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ConsumingTheLinkSignsInOnceAndReturnsToTheRequestedPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;
        using var client = api.CreateUsersClient();
        var email = MagicLinkHost.NewEmail("entra");
        var token = await client.RequestTokenAsync(mailpit, email, ct, returnTo: "/curitiba");

        using var consumed = await client.ConsumeMagicLinkAsync(token, ct);

        var body = await consumed.Content.ReadAsStringAsync(ct);
        consumed.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        using (var document = JsonDocument.Parse(body))
        {
            document.RootElement.GetProperty("returnTo").GetString().ShouldBe("/curitiba", body);
        }

        consumed.Headers.TryGetValues("Set-Cookie", out var cookies).ShouldBeTrue("o consumo não emitiu o cookie de sessão");
        cookies.ShouldContain(cookie => cookie.StartsWith(UsersApiClient.SessionCookieName + "=", StringComparison.Ordinal));
        client.SessionCookie.ShouldNotBeNullOrWhiteSpace();

        using (var me = await client.GetAsync(MeEndpointTests.MeRoute, ct))
        {
            var meBody = await me.Content.ReadAsStringAsync(ct);
            me.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(meBody));
            using var document = JsonDocument.Parse(meBody);
            document.RootElement.GetProperty("email").GetString().ShouldBe(email);
        }

        (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem().Method.ShouldBe("magic_link");

        using var anotherClient = api.CreateUsersClient();
        using var second = await anotherClient.ConsumeMagicLinkAsync(token, ct);
        await second.ShouldBeProblemAsync(HttpStatusCode.BadRequest, Invalid, ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ConsumeWithoutReturnToGoesBackToTheHomePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        var token = await client.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("sem-volta"), ct, returnTo: "//evil.com");

        using var consumed = await client.ConsumeMagicLinkAsync(token, ct);

        var body = await consumed.Content.ReadAsStringAsync(ct);
        consumed.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("returnTo").GetString().ShouldBe("/", "returnTo inseguro vira /");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExpiredOrRandomTokenIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now());
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, clock);
        await using var _ = api;
        using var client = api.CreateUsersClient();
        var token = await client.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("expira"), ct);

        clock.Advance(TimeSpan.FromMinutes(16));
        using var expired = await client.ConsumeMagicLinkAsync(token, ct);
        using var random = await client.ConsumeMagicLinkAsync(SessionTokens.NewRandom(), ct);

        await expired.ShouldBeProblemAsync(HttpStatusCode.BadRequest, Invalid, ct);
        await random.ShouldBeProblemAsync(HttpStatusCode.BadRequest, Invalid, ct);
        client.SessionCookie.ShouldBeNull("link inválido não emite cookie de sessão");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldBeEmpty("link inválido não cria sessão");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task P95OfTwentyConsumesIsAtMost300Milliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var __ = api;
        var tokens = new List<string>();
        using (var requester = api.CreateUsersClient())
        {
            for (var i = 0; i < 20; i++)
            {
                tokens.Add(await requester.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail($"p95-{i}"), ct));
            }
        }

        // Aquecimento: o primeiro consumo do host carrega o caminho (JIT, pool de conexões) e não entra na medida.
        using (var warm = api.CreateUsersClient())
        {
            using var warmUp = await warm.ConsumeMagicLinkAsync(
                await warm.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("aquece"), ct), ct);
            warmUp.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await warmUp.Content.ReadAsStringAsync(ct)));
        }

        var durations = new List<TimeSpan>();
        foreach (var token in tokens)
        {
            using var client = api.CreateUsersClient();
            var csrf = await client.CsrfTokenAsync(ct);
            var watch = Stopwatch.StartNew();
            using var response = await client.SendAsync(
                HttpMethod.Post,
                MagicLinkHost.ConsumeRoute,
                ct,
                System.Net.Http.Json.JsonContent.Create(new { token }),
                csrf);
            watch.Stop();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct)));
            durations.Add(watch.Elapsed);
        }

        MagicLinkHost.P95(durations).ShouldBeLessThanOrEqualTo(
            TimeSpan.FromMilliseconds(300),
            $"p95 de 20 consumos: {MagicLinkHost.P95(durations).TotalMilliseconds:F0} ms");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TwoSimultaneousConsumesOfTheSameTokenGiveExactlyOneSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;

        for (var round = 0; round < 5; round++)
        {
            string token;
            using (var requester = api.CreateUsersClient())
            {
                token = await requester.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail($"corrida-{round}"), ct);
            }

            using var first = api.CreateUsersClient();
            using var second = api.CreateUsersClient();
            var firstCsrf = await first.CsrfTokenAsync(ct);
            var secondCsrf = await second.CsrfTokenAsync(ct);

            var responses = await Task.WhenAll(
                first.SendAsync(HttpMethod.Post, MagicLinkHost.ConsumeRoute, ct, System.Net.Http.Json.JsonContent.Create(new { token }), firstCsrf),
                second.SendAsync(HttpMethod.Post, MagicLinkHost.ConsumeRoute, ct, System.Net.Http.Json.JsonContent.Create(new { token }), secondCsrf));
            try
            {
                var statuses = responses.Select(response => response.StatusCode).Order().ToList();
                var bodies = string.Join(" | ", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync(ct))));
                statuses.ShouldBe([HttpStatusCode.OK, HttpStatusCode.BadRequest], $"rodada {round}: {UsersApiClient.Excerpt(bodies)}");
                await responses.Single(response => response.StatusCode == HttpStatusCode.BadRequest)
                    .ShouldBeProblemAsync(HttpStatusCode.BadRequest, Invalid, ct);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }

        (await UsersDatabase.SessionsAsync(database, ct)).Count.ShouldBe(5, "uma sessão por link, nunca duas");
    }

    /// <summary>Agora, em segundos inteiros (o relógio falso começa perto do real: cookies e chaves seguem coerentes).</summary>
    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }
}
