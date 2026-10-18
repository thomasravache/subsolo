using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-03 — um retorno com <c>state</c> adulterado e outro com <c>error=access_denied</c> (o fã cancelou no
/// provedor) chegam à API e terminam em 302 para <c>/entrar?erro=google</c>, sem sessão nem usuário criados.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleCallbackRejectionTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    [Fact]
    [Trait("Spec", "SPEC-0020:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TamperedStateGoesBackToTheSignInPageWithTheGoogleError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        var flow = await client.SignInWithGoogleAsync(
            google,
            FakeGoogleProvider.Issuers.Verified,
            "/curitiba",
            ct,
            tamper: callback => callback.With("state", Tamper(callback.Parameters["state"])));
        using var response = flow.Callback;

        await ShouldBeRejectedAsync(response, client, database, "state adulterado", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AccessDeniedFromTheProviderGoesBackToTheSignInPageWithTheGoogleError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        // O provedor responde ao redirect_uri com o erro do OAuth (o fã cancelou), com o state válido e sem código.
        var flow = await client.SignInWithGoogleAsync(
            google,
            FakeGoogleProvider.Issuers.Verified,
            "/curitiba",
            ct,
            tamper: callback => callback
                .With("code", null)
                .With("error", "access_denied")
                .With("error_description", "O usuário cancelou o login."));
        using var response = flow.Callback;

        await ShouldBeRejectedAsync(response, client, database, "access_denied", ct);
    }

    private static string Tamper(string state) =>
        state.Length > 8 ? state[..^4] + (state[^4..] == "AAAA" ? "BBBB" : "AAAA") : state + "x";

    private static async Task ShouldBeRejectedAsync(
        HttpResponseMessage response, UsersApiClient client, string database, string scenario, CancellationToken ct)
    {
        response.RedirectTarget().ShouldBe(GoogleHost.ProviderErrorRedirect, scenario);
        client.SessionCookie.ShouldBeNull($"{scenario}: nenhuma sessão emitida");
        (await UsersDatabase.UsersAsync(database, ct)).ShouldBeEmpty($"{scenario}: nenhum usuário criado");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldBeEmpty($"{scenario}: nenhuma sessão criada");
    }
}
