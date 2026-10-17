using System.Security.Claims;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-01 — com PostgreSQL real e a API, duas entradas do mesmo e-mail de admin pelo <c>ISignInService</c>
/// (uma com outra caixa e espaços) deixam no schema <c>users</c> um usuário com os papéis <c>fan</c> e <c>admin</c>,
/// duas sessões com o id guardado só como hash (nem o valor do cookie nem o id da sessão que ele leva aparecem na
/// tabela) e uma única mensagem <c>UserRegistered</c> na outbox.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SignInPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0014:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwoSignInsKeepOneAdminUserTwoHashedSessionsAndOneUserRegisteredInTheOutbox()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();

        var first = await client.SignInAsync(" Admin@Subsolo.TEST ", ct);
        var second = await client.SignInAsync(UsersApiFactory.AdminEmail, ct);

        first.IsNewUser.ShouldBeTrue();
        second.IsNewUser.ShouldBeFalse();
        second.UserId.ShouldBe(first.UserId);
        second.SessionCookie.ShouldNotBe(first.SessionCookie, "cada entrada emite uma sessão nova");

        var user = (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem();
        user.Id.ShouldBe(first.UserId);
        user.Email.ShouldBe(UsersApiFactory.AdminEmail);
        user.DisplayName.ShouldBeNull();
        user.Roles.ShouldContain("fan");
        user.Roles.ShouldContain("admin");

        var sessions = await UsersDatabase.SessionsAsync(database, ct);
        sessions.Count.ShouldBe(2);
        sessions.ShouldAllBe(session => session.UserId == first.UserId && session.Method == "magic_link");

        (await UsersDatabase.OutboxCountAsync(database, "UserRegistered", ct)).ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task SessionIdFromTheCookieIsNotStoredInPlainText()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();

        var signedIn = await client.SignInAsync("fulano@subsolo.test", ct);

        var ticket = api.UnprotectSessionCookie(signedIn.SessionCookie);
        ticket.ShouldNotBeNull("o cookie de sessão não abre com a proteção de dados do host");
        var claims = ticket.Principal.Claims.ToList();
        claims.ShouldContain(
            claim => claim.Value == signedIn.UserId.ToString(),
            $"o cookie leva o id do usuário; claims: {Describe(claims)}");
        var others = claims.Where(claim => claim.Value != signedIn.UserId.ToString()).ToList();
        others.ShouldNotBeEmpty($"o cookie leva o id da sessão; claims: {Describe(claims)}");

        var session = (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem();
        session.Json.ShouldNotContain(signedIn.SessionCookie, Case.Sensitive, "o valor do cookie aparece na tabela");
        foreach (var claim in others)
        {
            session.Json.ShouldNotContain(claim.Value, Case.Insensitive, $"o claim {claim.Type} do cookie aparece em claro na tabela");
            if (Guid.TryParse(claim.Value, out var id))
            {
                session.Json.ShouldNotContain(id.ToString("N"), Case.Insensitive, $"o claim {claim.Type} do cookie aparece em claro na tabela");
            }
        }
    }

    private static string Describe(IEnumerable<Claim> claims) =>
        string.Join(", ", claims.Select(claim => claim.Type));
}
