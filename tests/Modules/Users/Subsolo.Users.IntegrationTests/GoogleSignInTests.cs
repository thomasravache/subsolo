using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-01 — com a API, o PostgreSQL e o provedor OIDC falso reais, o fluxo do navegador (início → authorize do
/// provedor com login automático de <c>fa@subsolo.test</c> verificado → retorno) volta para o <c>returnTo</c> com
/// <c>Set-Cookie __Host-subsolo</c> (sessão nova, método <c>google</c>), <c>/me</c> responde 200 com o e-mail e o
/// cookie externo foi apagado; quem já entrou pelo link mágico com o mesmo e-mail cai no mesmo usuário; o retorno leva
/// ≤ 500 ms p95 em 10 rodadas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleSignInTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    private static readonly TimeSpan CallbackBudget = TimeSpan.FromMilliseconds(500);

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task VerifiedEmailSignsInAndGoesBackToTheReturnTo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        var flow = await client.SignInWithGoogleAsync(google, FakeGoogleProvider.Issuers.Verified, "/curitiba", ct);
        using var callback = flow.Callback;

        callback.RedirectTarget().ShouldBe("/curitiba", "volta para o returnTo saneado");
        var issued = callback.SetCookies(UsersApiClient.SessionCookieName).ShouldHaveSingleItem("Set-Cookie __Host-subsolo no retorno");
        issued.ShouldContain("httponly", Case.Insensitive);
        issued.ShouldContain("secure", Case.Insensitive);
        client.SessionCookie.ShouldNotBeNullOrWhiteSpace();
        client.Cookie(GoogleHost.ExternalCookieName).ShouldBeNull($"o cookie externo {GoogleHost.ExternalCookieName} ficou depois do retorno");

        using var me = await client.GetAsync("/api/v1/me", ct);
        var body = await me.Content.ReadAsStringAsync(ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("email").GetString().ShouldBe(FakeGoogleProvider.VerifiedEmail);

        var user = (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem();
        user.Email.ShouldBe(FakeGoogleProvider.VerifiedEmail);
        user.DisplayName.ShouldBeNull("nome do Google descartado: só o e-mail é usado");
        var session = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        session.UserId.ShouldBe(user.Id);
        session.Method.ShouldBe("google");
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AccountCreatedByTheMagicLinkIsTheSameUserWithANewGoogleSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var magicLink = api.CreateUsersClient();
        var existing = await magicLink.SignInAsync(FakeGoogleProvider.VerifiedEmail, ct);
        existing.IsNewUser.ShouldBeTrue();

        using var client = api.CreateUsersClient();
        var flow = await client.SignInWithGoogleAsync(google, FakeGoogleProvider.Issuers.Verified, "/", ct);
        using var callback = flow.Callback;

        callback.RedirectTarget().ShouldBe("/");
        client.SessionCookie.ShouldNotBeNullOrWhiteSpace();
        client.SessionCookie.ShouldNotBe(existing.SessionCookie, "a entrada com Google cria uma sessão nova");
        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem().Id.ShouldBe(existing.UserId, "mesmo usuário pelo e-mail verificado");
        var sessions = await UsersDatabase.SessionsAsync(database, ct);
        sessions.Count.ShouldBe(2);
        sessions.ShouldAllBe(session => session.UserId == existing.UserId);
        sessions.Select(session => session.Method).ShouldBe(["magic_link", "google"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task CallbackTakesAtMost500MillisecondsAtP95()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;

        // Uma rodada de aquecimento (JIT e primeira conexão com o provedor), fora da medida.
        using (var warmUp = api.CreateUsersClient())
        {
            var first = await warmUp.SignInWithGoogleAsync(google, FakeGoogleProvider.Issuers.Verified, "/", ct);
            first.Callback.Dispose();
        }

        var durations = new List<TimeSpan>();
        for (var round = 1; round <= 10; round++)
        {
            using var client = api.CreateUsersClient();
            var flow = await client.SignInWithGoogleAsync(google, FakeGoogleProvider.Issuers.Verified, "/curitiba", ct);
            using var callback = flow.Callback;
            callback.RedirectTarget().ShouldBe("/curitiba", $"rodada {round}");
            durations.Add(flow.CallbackDuration);
        }

        MagicLinkHost.P95(durations).ShouldBeLessThanOrEqualTo(
            CallbackBudget, $"retorno do provedor até o cookie (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
    }
}
