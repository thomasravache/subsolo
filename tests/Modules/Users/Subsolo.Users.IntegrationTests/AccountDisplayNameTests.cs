using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0019 IT-01 — com o PostgreSQL real e um usuário logado, <c>PUT /api/v1/me</c> muda o nome (sem os espaços das
/// pontas) e depois o remove (<see langword="null"/> e <c>""</c>): cada resposta é o corpo do <c>GET /me</c>
/// (<c>{id, email, displayName, roles}</c>) e o <c>GET /me</c> e a tabela refletem a mudança; 61 caracteres dão 400
/// <c>users.invalid_display_name</c> e o nome continua.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountDisplayNameTests(PostgresFixture postgres)
{
    private const string Email = "fulano@subsolo.test";

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task PutMeChangesAndThenRemovesTheName()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(Email, ct);

        using (var renamed = await client.RenameAsync("  Caio Grind  ", ct))
        {
            await ShouldBeMeAsync(renamed, signedIn.UserId, "Caio Grind", ct);
        }

        (await MeDisplayNameAsync(client, ct)).ShouldBe("Caio Grind");
        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem().DisplayName.ShouldBe("Caio Grind");

        using (var removed = await client.RenameAsync(null, ct))
        {
            await ShouldBeMeAsync(removed, signedIn.UserId, null, ct);
        }

        (await MeDisplayNameAsync(client, ct)).ShouldBeNull();
        (await UsersDatabase.UsersAsync(database, ct)).ShouldHaveSingleItem().DisplayName.ShouldBeNull();

        using (var again = await client.RenameAsync("Caio", ct))
        {
            await ShouldBeMeAsync(again, signedIn.UserId, "Caio", ct);
        }

        using (var emptied = await client.RenameAsync(string.Empty, ct))
        {
            await ShouldBeMeAsync(emptied, signedIn.UserId, null, ct);
        }

        (await MeDisplayNameAsync(client, ct)).ShouldBeNull("vazio remove o nome");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task SixtyOneCharactersReturn400AndKeepTheName()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(Email, ct);
        await UsersDatabase.SetDisplayNameAsync(database, signedIn.UserId, "Caio Grind", ct);

        using var response = await client.RenameAsync(new string('a', 61), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "users.invalid_display_name", ct);
        response.ShouldBeNoStore();
        (await MeDisplayNameAsync(client, ct)).ShouldBe("Caio Grind", "o nome antigo continua");
    }

    private static async Task ShouldBeMeAsync(HttpResponseMessage response, Guid userId, string? displayName, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"PUT /api/v1/me: {UsersApiClient.Excerpt(body)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        response.ShouldBeNoStore();
        using var document = JsonDocument.Parse(body);
        var me = document.RootElement;
        string.Join(",", me.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal))
            .ShouldBe("displayName,email,id,roles", body);
        me.GetProperty("id").GetGuid().ShouldBe(userId);
        me.GetProperty("email").GetString().ShouldBe(Email);
        me.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["fan"]);
        if (displayName is null)
        {
            me.GetProperty("displayName").ValueKind.ShouldBe(JsonValueKind.Null, body);
        }
        else
        {
            me.GetProperty("displayName").GetString().ShouldBe(displayName, body);
        }
    }

    private static async Task<string?> MeDisplayNameAsync(UsersApiClient client, CancellationToken ct)
    {
        using var me = await client.GetAsync(AccountRequests.MeRoute, ct);
        var body = await me.Content.ReadAsStringAsync(ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        using var document = JsonDocument.Parse(body);
        var name = document.RootElement.GetProperty("displayName");
        return name.ValueKind == JsonValueKind.Null ? null : name.GetString();
    }
}
