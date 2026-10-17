using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-08 — os dois POST do link mágico sem <c>X-CSRF</c> respondem 400 <c>security.csrf_invalid</c> e nada
/// é gravado nem enviado: o pedido não grava link nem manda e-mail; o consumo não gasta o token nem cria sessão.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkAntiforgeryTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    private const string CsrfInvalid = "security.csrf_invalid";

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task RequestWithoutCsrfIsRejectedAndNothingIsStoredOrSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;
        using var client = api.CreateUsersClient();
        var email = MagicLinkHost.NewEmail("sem-csrf");

        using var response = await client.RequestMagicLinkAsync(email, ct, withCsrf: false);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, CsrfInvalid, ct);
        (await MagicLinkDatabase.RowsAsync(database, ct)).ShouldBeEmpty("pedido sem X-CSRF não grava link");
        (await MagicLinkHost.WaitForMessagesAsync(mailpit, email, 1, ct, TimeSpan.FromSeconds(2)))
            .ShouldBeEmpty("pedido sem X-CSRF não manda e-mail");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task ConsumeWithoutCsrfIsRejectedAndTheTokenStaysUsable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;
        using var client = api.CreateUsersClient();
        var token = await client.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("consumo-sem-csrf"), ct);

        using var rejected = await client.ConsumeMagicLinkAsync(token, ct, withCsrf: false);

        await rejected.ShouldBeProblemAsync(HttpStatusCode.BadRequest, CsrfInvalid, ct);
        client.SessionCookie.ShouldBeNull("consumo sem X-CSRF não emite cookie");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldBeEmpty("consumo sem X-CSRF não cria sessão");

        using var accepted = await client.ConsumeMagicLinkAsync(token, ct);
        accepted.StatusCode.ShouldBe(
            HttpStatusCode.OK, $"o token foi gasto pelo consumo rejeitado: {UsersApiClient.Excerpt(await accepted.Content.ReadAsStringAsync(ct))}");
    }
}
