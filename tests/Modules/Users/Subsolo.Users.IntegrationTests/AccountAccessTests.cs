using System.Net;
using System.Net.Http.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0019 IT-05 — sem cookie, <c>PUT /api/v1/me</c>, <c>GET /api/v1/me/export</c> e <c>DELETE /api/v1/me</c> dão 401
/// <c>auth.required</c>; toda resposta dos três (200, 204, 400 e 401) tem <c>Cache-Control: no-store</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountAccessTests(PostgresFixture postgres)
{
    private const string Email = "fulano@subsolo.test";

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousGets401AuthRequiredWithNoStoreOnTheThreeEndpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();

        using (var rename = await client.RenameAsync("Caio Grind", ct))
        {
            await rename.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
            rename.ShouldBeNoStore();
        }

        using (var export = await client.ExportAsync(ct))
        {
            await export.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
            export.ShouldBeNoStore();
        }

        using (var delete = await client.DeleteAccountAsync(Email, ct))
        {
            await delete.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
            delete.ShouldBeNoStore();
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task EveryResponseOfTheThreeEndpointsIsNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync(Email, ct);
        var responses = new List<(string Name, HttpStatusCode Expected, HttpResponseMessage Response)>();
        try
        {
            responses.Add(("PUT 200", HttpStatusCode.OK, await client.RenameAsync("Caio Grind", ct)));
            responses.Add(("PUT 400 nome", HttpStatusCode.BadRequest, await client.RenameAsync(new string('a', 61), ct)));
            responses.Add(("PUT 400 csrf", HttpStatusCode.BadRequest, await client.RenameAsync("Caio", ct, withCsrf: false)));
            responses.Add(("GET export 200", HttpStatusCode.OK, await client.ExportAsync(ct)));
            responses.Add(("DELETE 400 e-mail", HttpStatusCode.BadRequest, await client.DeleteAccountAsync("outro@subsolo.test", ct)));
            responses.Add(("DELETE 400 csrf", HttpStatusCode.BadRequest, await client.DeleteAccountAsync(Email, ct, withCsrf: false)));
            responses.Add(("DELETE 204", HttpStatusCode.NoContent, await client.DeleteAccountAsync(Email, ct)));
            var token = await client.CsrfTokenAsync(ct);
            responses.Add(("PUT 401", HttpStatusCode.Unauthorized, await client.SendAsync(
                HttpMethod.Put, AccountRequests.MeRoute, ct, JsonContent.Create(new { displayName = "x" }), token)));

            foreach (var (name, expected, response) in responses)
            {
                response.StatusCode.ShouldBe(
                    expected, $"{name}: {UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct))}");
                response.ShouldBeNoStore();
            }
        }
        finally
        {
            foreach (var (_, _, response) in responses)
            {
                response.Dispose();
            }
        }
    }
}
