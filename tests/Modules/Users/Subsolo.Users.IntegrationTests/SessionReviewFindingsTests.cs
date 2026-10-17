using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-09 (§7.6 a, b, f; achados da revisão da SPEC-0014) — no host real com <see cref="FakeTimeProvider"/>:
/// (a) a validade do cookie acompanha a sessão do banco — quem entra, usa até o dia 14 e volta no dia 40 continua
/// logado, e quem volta no dia 44 e 1 h (30 dias sem uso) não; (b) dois consumos simultâneos de links diferentes do
/// mesmo e-mail novo dão 200 nos dois e um único usuário (sem 500 pela violação do índice único); (f) o 429 de
/// <c>/me</c> sai com <c>Cache-Control: no-store</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SessionReviewFindingsTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    [Fact]
    [Trait("Spec", "SPEC-0016:IT-09")]
    [Trait("Kind", "Integration")]
    public async Task UserActiveUntilDay14IsStillSignedInOnDay40()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = Now();
        var clock = new FakeTimeProvider(start);
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, clock);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        await client.SignInAsync(MagicLinkHost.NewEmail("volta-dia-40"), ct);

        clock.SetUtcNow(start + TimeSpan.FromDays(14));
        await MeShouldBeAsync(client, HttpStatusCode.OK, "no dia 14", ct);

        clock.SetUtcNow(start + TimeSpan.FromDays(40));
        await MeShouldBeAsync(
            client, HttpStatusCode.OK, "no dia 40 (a sessão do banco vale até o dia 44; o cookie não pode expirar antes)", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-09")]
    [Trait("Kind", "Integration")]
    public async Task UserActiveUntilDay14IsSignedOutAfterDay44()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = Now();
        var clock = new FakeTimeProvider(start);
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, clock);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(MagicLinkHost.NewEmail("volta-dia-44"), ct);

        clock.SetUtcNow(start + TimeSpan.FromDays(14));
        await MeShouldBeAsync(client, HttpStatusCode.OK, "no dia 14", ct);

        clock.SetUtcNow(start + TimeSpan.FromDays(44) + TimeSpan.FromHours(1));
        using var response = await client.GetAsync(
            MeEndpointTests.MeRoute, ct, sessionCookie: client.SessionCookie ?? signedIn.SessionCookie);
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-09")]
    [Trait("Kind", "Integration")]
    public async Task TwoSimultaneousFirstSignInsOfTheSameNewEmailCreateASingleUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;
        var email = MagicLinkHost.NewEmail("primeira-entrada");
        string firstToken;
        string secondToken;
        using (var requester = api.CreateUsersClient())
        {
            firstToken = await requester.RequestTokenAsync(mailpit, email, ct);
            secondToken = await requester.RequestTokenAsync(mailpit, email, ct);
        }

        using var first = api.CreateUsersClient();
        using var second = api.CreateUsersClient();
        var firstCsrf = await first.CsrfTokenAsync(ct);
        var secondCsrf = await second.CsrfTokenAsync(ct);

        var responses = await Task.WhenAll(
            first.SendAsync(HttpMethod.Post, MagicLinkHost.ConsumeRoute, ct, JsonContent.Create(new { token = firstToken }), firstCsrf),
            second.SendAsync(HttpMethod.Post, MagicLinkHost.ConsumeRoute, ct, JsonContent.Create(new { token = secondToken }), secondCsrf));
        try
        {
            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(
                    HttpStatusCode.OK,
                    $"primeira entrada simultânea: {UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct))}");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem("um único usuário por e-mail").Email.ShouldBe(email);
        (await UsersDatabase.SessionsAsync(database, ct)).Count.ShouldBe(2, "uma sessão para cada entrada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-09")]
    [Trait("Kind", "Integration")]
    public async Task TooManyRequestsFromMeIsNotStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Security:RateLimiting:default:PermitLimit"] = "2",
            ["Security:RateLimiting:default:Window"] = "00:01:00",
        };
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, overrides: overrides);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        await client.SignInAsync(MagicLinkHost.NewEmail("me-429"), ct);
        client.RemoteIp = "198.51.100.91";

        for (var call = 1; call <= 2; call++)
        {
            await MeShouldBeAsync(client, HttpStatusCode.OK, $"chamada {call} de 2", ct);
        }

        using var limited = await client.GetAsync(MeEndpointTests.MeRoute, ct);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        limited.ShouldBeNoStore();
    }

    private static async Task MeShouldBeAsync(UsersApiClient client, HttpStatusCode status, string when, CancellationToken ct)
    {
        using var response = await client.GetAsync(MeEndpointTests.MeRoute, ct);
        response.StatusCode.ShouldBe(status, $"/me {when}: {UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct))}");
    }

    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }
}
