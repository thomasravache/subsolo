using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-08 — com a sessão real (cookie do <c>SignInAsync</c>, papéis lidos da sessão validada), um usuário só
/// <c>fan</c> num endpoint de teste <c>RequireAuthorization(admin)</c> recebe 403 <c>code=auth.forbidden</c>; com o
/// papel <c>admin</c> (e-mail em <c>Users:AdminEmails</c>), 200.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdminRoleTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0014:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task FanOnAdminEndpointReturns403AuthForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync("fa@subsolo.test", ct);

        using var response = await client.GetAsync(UsersTestEndpoints.Admin, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task AdminOnAdminEndpointIsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync(UsersApiFactory.AdminEmail, ct);

        using var response = await client.GetAsync(UsersTestEndpoints.Admin, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct)));
    }
}
