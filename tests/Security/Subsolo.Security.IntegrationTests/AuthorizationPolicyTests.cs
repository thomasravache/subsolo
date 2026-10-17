using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0010 IT-02 — autorização negada por padrão (ADR-0003): sem login, um endpoint protegido (pela política de
/// fallback ou pelas políticas <c>fan</c>, <c>artist</c> e <c>admin</c>) responde 401 ProblemDetails
/// <c>code=auth.required</c>; um usuário <c>fan</c> (dublê <see cref="TestAuthHandler"/>) num endpoint
/// <c>artist</c> ou <c>admin</c> recebe 403 <c>code=auth.forbidden</c>; com o papel certo, passa.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AuthorizationPolicyTests(PostgresFixture postgres)
{
    [Theory]
    [Trait("Spec", "SPEC-0010:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(SecurityTestEndpoints.Fallback)]
    [InlineData(SecurityTestEndpoints.Fan)]
    [InlineData(SecurityTestEndpoints.Artist)]
    [InlineData(SecurityTestEndpoints.Admin)]
    public async Task AnonymousOnProtectedEndpointReturns401AuthRequired(string route)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(route, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0010:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(SecurityTestEndpoints.Artist)]
    [InlineData(SecurityTestEndpoints.Admin)]
    public async Task FanOnArtistOrAdminEndpointReturns403AuthForbidden(string route)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "fan");

        using var response = await client.GetAsync(route, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0010:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(SecurityTestEndpoints.Fallback, "fan")]
    [InlineData(SecurityTestEndpoints.Fan, "fan")]
    [InlineData(SecurityTestEndpoints.Artist, "artist")]
    [InlineData(SecurityTestEndpoints.Admin, "admin")]
    public async Task UserWithTheRequiredRoleIsAllowed(string route, string role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        using var response = await client.GetAsync(route, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
    }
}
