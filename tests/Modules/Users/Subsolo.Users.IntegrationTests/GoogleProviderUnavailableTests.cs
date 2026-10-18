using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0020 IT-04 — sem <c>Users:Google</c>, a API sobe, <c>GET /auth/providers</c> responde <c>{ google: false }</c> e
/// <c>GET /auth/google</c> responde 404 <c>users.provider_disabled</c>; com o Google configurado,
/// <c>{ google: true }</c>; com a authority apontando para um endereço sem servidor, o início devolve 302 para
/// <c>/entrar?erro=google</c> em ≤ 11 s, e o link por e-mail continua funcionando.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleProviderUnavailableTests(PostgresFixture postgres, MailpitFixture mailpit, FakeGoogleProvider google)
{
    private static readonly TimeSpan UnavailableBudget = TimeSpan.FromSeconds(11);

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WithoutConfigurationGoogleIsOffAndTheStartIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await GoogleHost.StartAsync(postgres, mailpit, overrides: null, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        (await ProvidersAsync(client, ct)).ShouldBeFalse("sem Users:Google, providers.google = false");

        using var start = await client.StartGoogleAsync("/curitiba", ct);
        await start.ShouldBeProblemAsync(HttpStatusCode.NotFound, "users.provider_disabled", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WithConfigurationGoogleIsOn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await GoogleHost.StartAsync(
            postgres, mailpit, GoogleHost.GoogleSettings(google, FakeGoogleProvider.Issuers.Verified), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        (await ProvidersAsync(client, ct)).ShouldBeTrue("com Authority e ClientId, providers.google = true");
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task UnreachableAuthorityGoesBackToTheSignInPageAndTheMagicLinkStillWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var deadAuthority = string.Create(
            CultureInfo.InvariantCulture, $"http://127.0.0.1:{MagicLinkHost.ClosedPort()}/{FakeGoogleProvider.Issuers.Verified}");
        var (api, database) = await GoogleHost.StartAsync(postgres, mailpit, GoogleHost.GoogleSettings(deadAuthority), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        var watch = Stopwatch.StartNew();
        using var start = await client.StartGoogleAsync("/curitiba", ct);
        watch.Stop();

        start.RedirectTarget().ShouldBe(GoogleHost.ProviderErrorRedirect, "provedor fora do ar");
        watch.Elapsed.ShouldBeLessThanOrEqualTo(UnavailableBudget, $"início com o provedor fora do ar: {watch.Elapsed}");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldBeEmpty();

        // O link por e-mail segue funcionando com o Google fora do ar.
        var email = MagicLinkHost.NewEmail("google-fora");
        var token = await client.RequestTokenAsync(mailpit, email, ct, "/curitiba");
        using var consumed = await client.ConsumeMagicLinkAsync(token, ct);
        consumed.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await consumed.Content.ReadAsStringAsync(ct)));
        client.SessionCookie.ShouldNotBeNullOrWhiteSpace("o link mágico emitiu a sessão");
    }

    private static async Task<bool> ProvidersAsync(UsersApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(GoogleHost.ProvidersRoute, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {GoogleHost.ProvidersRoute}: {UsersApiClient.Excerpt(body)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        using var document = JsonDocument.Parse(body);
        var google = document.RootElement.GetProperty("google");
        google.ValueKind.ShouldBeOneOf([JsonValueKind.True, JsonValueKind.False], $"google é booleano: {body}");
        return google.GetBoolean();
    }
}
