using System.Net;
using Microsoft.Net.Http.Headers;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-03 — com cookie válido e o token X-CSRF, <c>POST /api/v1/auth/logout</c> responde 204 com
/// <c>Cache-Control: no-store</c>, apaga o cookie <c>__Host-subsolo</c>, grava <c>revoked_at</c> e o mesmo cookie em
/// <c>/me</c> volta 401. IT-05 — sem X-CSRF, 400 <c>security.csrf_invalid</c> e a sessão segue válida.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class LogoutTests(PostgresFixture postgres)
{
    public const string LogoutRoute = "/api/v1/auth/logout";

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task LogoutRevokesTheSessionAndDeletesTheCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync("fulano@subsolo.test", ct);
        var token = await client.CsrfTokenAsync(ct);

        using var response = await client.SendAsync(HttpMethod.Post, LogoutRoute, ct, csrfToken: token);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, UsersApiClient.Excerpt(body));
        response.ShouldBeNoStore();
        response.Headers.TryGetValues("Set-Cookie", out var setCookies).ShouldBeTrue("o logout não apagou o cookie de sessão");
        var deleted = SetCookieHeaderValue.ParseList([.. setCookies!])
            .Where(cookie => cookie.Name.Value == UsersApiClient.SessionCookieName)
            .ShouldHaveSingleItem($"o logout não apagou o cookie {UsersApiClient.SessionCookieName}");
        deleted.Value.Value.ShouldBeNullOrEmpty();
        (deleted.Expires is { } expires && expires < DateTimeOffset.UtcNow || deleted.MaxAge is { } maxAge && maxAge <= TimeSpan.Zero)
            .ShouldBeTrue($"Set-Cookie não expira o cookie: {deleted}");
        client.SessionCookie.ShouldBeNull();

        var session = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        session.RevokedAtUtc.ShouldNotBeNull("revoked_at não foi gravado");

        using var me = await client.GetAsync(MeRoute, ct, sessionCookie: signedIn.SessionCookie);
        await me.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task LogoutWithoutSessionReturns401AuthRequiredWithNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var token = await client.CsrfTokenAsync(ct);

        using var response = await client.SendAsync(HttpMethod.Post, LogoutRoute, ct, csrfToken: token);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        response.ShouldBeNoStore();
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task LogoutWithoutCsrfReturns400AndKeepsTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync("fulano@subsolo.test", ct);

        using var response = await client.SendAsync(HttpMethod.Post, LogoutRoute, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem().RevokedAtUtc.ShouldBeNull();
        using var me = await client.GetAsync(MeRoute, ct, sessionCookie: signedIn.SessionCookie);
        me.StatusCode.ShouldBe(HttpStatusCode.OK, "a sessão continua válida depois do logout recusado");
    }

    private const string MeRoute = MeEndpointTests.MeRoute;
}
