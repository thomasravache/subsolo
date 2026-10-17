using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-01 — com a API, o PostgreSQL e o Mailpit reais, <c>POST /api/v1/auth/magic-link</c> para um e-mail
/// com conta e para outro sem conta responde 202 com o mesmo corpo (não revela quem tem conta); as duas mensagens
/// chegam ao Mailpit com o link <c>{App:PublicUrl}/entrar/confirmar?token=…</c>; a tabela guarda só o hash dos tokens;
/// a conta do e-mail novo só nasce no consumo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkRequestTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    [Fact]
    [Trait("Spec", "SPEC-0016:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ExistingAndNewEmailGetTheSameResponseAndTheLinkByEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var _ = api;
        using var client = api.CreateUsersClient();
        var existing = MagicLinkHost.NewEmail("com-conta");
        var unknown = MagicLinkHost.NewEmail("sem-conta");
        using (var setup = api.CreateUsersClient())
        {
            await setup.SignInAsync(existing, ct);
        }

        using var forExisting = await client.RequestMagicLinkAsync(existing, ct);
        using var forUnknown = await client.RequestMagicLinkAsync(unknown, ct);

        await forExisting.ShouldBeAcceptedAsync(ct);
        await forUnknown.ShouldBeAcceptedAsync(ct);
        (await forUnknown.Content.ReadAsStringAsync(ct)).ShouldBe(
            await forExisting.Content.ReadAsStringAsync(ct), "a resposta não revela se a conta existe");
        forUnknown.Content.Headers.ContentType?.MediaType.ShouldBe(forExisting.Content.Headers.ContentType?.MediaType);

        var tokens = new List<string>();
        foreach (var email in new[] { existing, unknown })
        {
            var message = (await MagicLinkHost.WaitForMessagesAsync(mailpit, email, 1, ct))
                .ShouldHaveSingleItem($"o e-mail do link para {email} não chegou (ou chegou mais de um)");
            message.Text.ShouldContain(MagicLinkHost.ConfirmPrefix);
            tokens.Add(MagicLinkHost.TokenOf(message));
        }

        var rows = await MagicLinkDatabase.RowsAsync(database, ct);
        rows.Count.ShouldBe(2, "um link gravado por pedido");
        foreach (var token in tokens)
        {
            token.Length.ShouldBeGreaterThanOrEqualTo(43, "o token tem 256 bits");
            rows.ShouldAllBe(row => !row.Contains(token, StringComparison.Ordinal), "o token do link foi gravado em claro");
            MagicLinkDatabase.RowOf(rows, token).ShouldNotBeNull("o hash SHA-256 do token não está na tabela");
        }

        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem("a conta do e-mail novo só nasce no consumo")
            .Email.ShouldBe(existing);
    }
}
