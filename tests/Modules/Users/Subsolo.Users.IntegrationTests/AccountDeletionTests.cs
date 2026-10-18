using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0019 IT-03 — com o PostgreSQL real, um usuário com 2 sessões e um link mágico pendente: <c>DELETE /api/v1/me</c>
/// com o e-mail certo (outra caixa e espaços) e <c>X-CSRF</c> responde 204, apaga o cookie, não deixa linha em
/// <c>users</c>, <c>sessions</c> nem <c>magic_link_tokens</c> desse e-mail (os do outro usuário ficam), põe
/// <c>UserDeleted</c> na outbox só com id e data, os dois cookies antigos dão 401 em <c>/me</c> e uma nova entrada com o
/// e-mail cria outro usuário, sem nome; p95 de 20 exclusões ≤ 500 ms. IT-04 — outro e-mail dá 400
/// <c>users.delete_confirmation_mismatch</c>; <c>PUT</c> e <c>DELETE</c> sem <c>X-CSRF</c> dão 400
/// <c>security.csrf_invalid</c>; em nenhum dos casos algo muda.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountDeletionTests(PostgresFixture postgres)
{
    private const string Email = "fulano@subsolo.test";
    private const string OtherEmail = "outra.pessoa@subsolo.test";
    private const string UserDeletedType = "UserDeleted";
    private const int Deletions = 20;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);

    private static readonly Dictionary<string, string> HighRateLimit = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "10000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task DeleteMeRemovesTheAccountTheSessionsAndTheLinksAndPublishesUserDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        using var otherDevice = api.CreateUsersClient();
        using var otherUser = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(Email, ct);
        var secondSession = await otherDevice.SignInAsync(Email, ct);
        var other = await otherUser.SignInAsync(OtherEmail, ct);
        await UsersDatabase.SetDisplayNameAsync(database, signedIn.UserId, "Caio Grind", ct);
        await MagicLinkDatabase.InsertPendingAsync(database, Email, ct);
        await MagicLinkDatabase.InsertPendingAsync(database, OtherEmail, ct);

        using var response = await client.DeleteAccountAsync(" FULANO@Subsolo.TEST ", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, UsersApiClient.Excerpt(body));
        response.ShouldBeNoStore();
        response.Headers.TryGetValues("Set-Cookie", out var setCookies).ShouldBeTrue("a exclusão não apagou o cookie de sessão");
        var deleted = SetCookieHeaderValue.ParseList([.. setCookies!])
            .Where(cookie => cookie.Name.Value == UsersApiClient.SessionCookieName)
            .ShouldHaveSingleItem($"a exclusão não apagou o cookie {UsersApiClient.SessionCookieName}");
        deleted.Value.Value.ShouldBeNullOrEmpty();
        (deleted.Expires is { } expires && expires < DateTimeOffset.UtcNow || deleted.MaxAge is { } maxAge && maxAge <= TimeSpan.Zero)
            .ShouldBeTrue($"Set-Cookie não expira o cookie: {deleted}");
        client.SessionCookie.ShouldBeNull();

        var users = await UsersDatabase.UsersAsync(database, ct);
        users.ShouldNotContain(user => user.Id == signedIn.UserId || user.Email == Email, "sobrou a linha em users");
        users.ShouldContain(user => user.Id == other.UserId, "o outro usuário foi apagado");
        var sessions = await UsersDatabase.SessionsAsync(database, ct);
        sessions.ShouldNotContain(session => session.UserId == signedIn.UserId, "sobrou sessão do usuário");
        sessions.ShouldContain(session => session.UserId == other.UserId, "a sessão do outro usuário foi apagada");
        var links = await MagicLinkDatabase.EmailsAsync(database, ct);
        links.ShouldNotContain(Email, "sobrou link mágico do e-mail");
        links.ShouldContain(OtherEmail, "o link do outro e-mail foi apagado");

        var message = (await UsersDatabase.OutboxContentsAsync(database, UserDeletedType, ct))
            .ShouldHaveSingleItem("UserDeleted não foi para a outbox (ou foi mais de uma vez)");
        message.ShouldNotContain(Email, Case.Insensitive, "o e-mail foi para a outbox");
        message.ShouldNotContain("Caio Grind", Case.Insensitive, "o nome foi para a outbox");
        using (var content = JsonDocument.Parse(message))
        {
            var properties = content.RootElement.EnumerateObject().ToDictionary(
                property => property.Name.ToUpperInvariant(), property => property.Value.Clone(), StringComparer.Ordinal);
            properties.Keys.ShouldBeSubsetOf(["USERID", "ID", "OCCURREDONUTC", "DELETEDATUTC"], $"UserDeleted com mais que id e data: {message}");
            properties.ShouldContainKey("USERID", message);
            properties["USERID"].GetGuid().ShouldBe(signedIn.UserId);
        }

        foreach (var cookie in new[] { signedIn.SessionCookie, secondSession.SessionCookie })
        {
            using var me = await client.GetAsync(AccountRequests.MeRoute, ct, sessionCookie: cookie);
            await me.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using var newClient = api.CreateUsersClient();
        var again = await newClient.SignInAsync(Email, ct);
        again.IsNewUser.ShouldBeTrue("a entrada depois da exclusão cria uma conta nova");
        again.UserId.ShouldNotBe(signedIn.UserId);
        using var newMe = await newClient.GetAsync(AccountRequests.MeRoute, ct);
        var newBody = await newMe.Content.ReadAsStringAsync(ct);
        newMe.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(newBody));
        using var newDocument = JsonDocument.Parse(newBody);
        newDocument.RootElement.GetProperty("displayName").ValueKind.ShouldBe(JsonValueKind.Null, "a conta nova nasce sem nome");
        (await UsersDatabase.SessionsAsync(database, ct)).Count(session => session.UserId == again.UserId)
            .ShouldBe(1, "a conta nova não herda as sessões antigas");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task DeletionIsUnderFiveHundredMillisecondsAtP95()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database, settings: HighRateLimit);
        var durations = new List<TimeSpan>();
        for (var i = 0; i <= Deletions; i++)
        {
            using var client = api.CreateUsersClient();
            var email = $"exclusao-{i}@subsolo.test";
            await client.SignInAsync(email, ct);
            await client.SignInAsync(email, ct);
            var token = await client.CsrfTokenAsync(ct);

            var (response, elapsed) = await AccountRequests.TimedAsync(() => client.SendAsync(
                HttpMethod.Delete, AccountRequests.MeRoute, ct, JsonContent.Create(new { confirmEmail = email }), token));
            using (response)
            {
                response.StatusCode.ShouldBe(
                    HttpStatusCode.NoContent, UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct)));
            }

            // A primeira exclusão aquece o host (JIT, pool de conexões) e fica fora da medida.
            if (i > 0)
            {
                durations.Add(elapsed);
            }
        }

        (await UsersDatabase.UsersAsync(database, ct)).ShouldBeEmpty();
        AccountRequests.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            Budget, $"p95 da exclusão (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0019:IT-04")]
    [Trait("Kind", "Integration")]
    [InlineData(OtherEmail)]
    [InlineData("fulano@subsolo.tes")]
    [InlineData("")]
    public async Task OtherEmailReturns400MismatchAndNothingChanges(string confirmEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(Email, ct);
        await MagicLinkDatabase.InsertPendingAsync(database, Email, ct);

        using var response = await client.DeleteAccountAsync(confirmEmail, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "users.delete_confirmation_mismatch", ct);
        response.ShouldBeNoStore();
        await ShouldBeIntactAsync(database, client, signedIn, ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task PutAndDeleteWithoutCsrfReturn400AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(Email, ct);
        await UsersDatabase.SetDisplayNameAsync(database, signedIn.UserId, "Caio Grind", ct);
        await MagicLinkDatabase.InsertPendingAsync(database, Email, ct);

        using (var rename = await client.RenameAsync("Outro Nome", ct, withCsrf: false))
        {
            await rename.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        using (var delete = await client.DeleteAccountAsync(Email, ct, withCsrf: false))
        {
            await delete.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem().DisplayName.ShouldBe("Caio Grind", "o nome não mudou");
        await ShouldBeIntactAsync(database, client, signedIn, ct);
    }

    private static async Task ShouldBeIntactAsync(string database, UsersApiClient client, SignedIn signedIn, CancellationToken ct)
    {
        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem().Id.ShouldBe(signedIn.UserId, "a conta foi apagada");
        (await UsersDatabase.SessionsAsync(database, ct)).ShouldHaveSingleItem().RevokedAtUtc.ShouldBeNull("a sessão mudou");
        (await MagicLinkDatabase.EmailsAsync(database, ct)).ShouldBe([Email], "o link pendente sumiu");
        (await UsersDatabase.OutboxCountAsync(database, UserDeletedType, ct)).ShouldBe(0, "UserDeleted foi para a outbox");
        using var me = await client.GetAsync(AccountRequests.MeRoute, ct, sessionCookie: signedIn.SessionCookie);
        me.StatusCode.ShouldBe(HttpStatusCode.OK, "a sessão continua válida");
    }
}
