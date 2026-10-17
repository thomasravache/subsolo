using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-02 — com um cookie emitido pelo <c>SignInAsync</c>, <c>GET /api/v1/me</c> responde 200 com
/// <c>{id, email, displayName, roles}</c> (§6) e <c>Cache-Control: no-store</c>; sem cookie, 401 ProblemDetails
/// <c>code=auth.required</c>, também sem cache.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MeEndpointTests(PostgresFixture postgres)
{
    public const string MeRoute = "/api/v1/me";

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task SignedInUserGetsTheContractAndNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync(" Fulano@Subsolo.TEST ", ct);

        using var response = await client.GetAsync(MeRoute, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        response.ShouldBeNoStore();
        using var document = JsonDocument.Parse(body);
        var me = document.RootElement;
        string.Join(",", me.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal))
            .ShouldBe("displayName,email,id,roles", body);
        me.GetProperty("id").GetGuid().ShouldBe(signedIn.UserId);
        me.GetProperty("email").GetString().ShouldBe("fulano@subsolo.test");
        me.GetProperty("displayName").ValueKind.ShouldBe(JsonValueKind.Null, body);
        me.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["fan"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AdminSeesTheAdminRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync(UsersApiFactory.AdminEmail, ct);

        using var response = await client.GetAsync(MeRoute, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .ShouldBe(["fan", "admin"], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task WithoutCookieReturns401AuthRequiredWithNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();

        using var response = await client.GetAsync(MeRoute, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        response.ShouldBeNoStore();
    }
}
