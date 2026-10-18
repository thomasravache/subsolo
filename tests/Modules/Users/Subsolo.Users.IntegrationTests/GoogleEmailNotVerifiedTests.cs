using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-02 — com o provedor falso devolvendo <c>email_verified=false</c> (e, noutra rodada, sem e-mail), o fluxo
/// termina em 302 para <c>/entrar?erro=google_email</c>, sem cookie de sessão e sem nenhuma linha nova em
/// <c>users</c> nem em <c>sessions</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleEmailNotVerifiedTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    [Theory]
    [Trait("Spec", "SPEC-0020:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(FakeGoogleProvider.Issuers.Unverified)]
    [InlineData(FakeGoogleProvider.Issuers.WithoutEmail)]
    public async Task WithoutAVerifiedEmailNoAccountNorSessionIsCreated(string issuer)
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await GoogleHost.StartAsync(postgres, mailpit, GoogleHost.GoogleSettings(google, issuer), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        var flow = await client.SignInWithGoogleAsync(google, issuer, "/curitiba", ct);
        using var callback = flow.Callback;

        callback.RedirectTarget().ShouldBe(GoogleHost.EmailNotVerifiedRedirect, issuer);
        callback.SetCookies(UsersApiClient.SessionCookieName)
            .Where(cookie => !cookie.StartsWith(UsersApiClient.SessionCookieName + "=;", StringComparison.Ordinal))
            .ShouldBeEmpty($"{issuer}: nenhuma sessão emitida");
        client.SessionCookie.ShouldBeNull(issuer);
        client.Cookie(GoogleHost.ExternalCookieName).ShouldBeNull($"{issuer}: o cookie externo ficou");
        (await UsersDatabase.UsersAsync(database, ct)).ShouldBeEmpty($"{issuer}: nenhum usuário criado");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldBeEmpty($"{issuer}: nenhuma sessão criada");
    }
}
