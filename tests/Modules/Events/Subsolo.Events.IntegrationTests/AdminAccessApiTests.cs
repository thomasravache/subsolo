using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0017 IT-04 — cada endpoint <c>/api/v1/admin/**</c> exige a política <c>admin</c>: anônimo recebe 401
/// <c>auth.required</c> e fã (sessão real sem o papel admin) recebe 403 <c>auth.forbidden</c>, mesmo com o X-CSRF
/// válido. IT-05 — o admin sem o cabeçalho X-CSRF em cada mutação recebe 400 <c>security.csrf_invalid</c> e nada muda.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdminAccessApiTests(PostgresFixture postgres)
{
    private static readonly object CityBody = new { name = "Cidade Proibida", state = "SP", timeZone = "America/Sao_Paulo", slug = (string?)null };
    private static readonly object CityUpdate = new { name = "Cidade Proibida", state = "SP", timeZone = "America/Sao_Paulo" };
    private static readonly object StyleBody = new { name = "Proibido" };

    /// <summary>Os endpoints de admin do §6: método, rota e corpo.</summary>
    public static readonly TheoryData<string, string, string> Endpoints = new()
    {
        { "GET", "/api/v1/admin/cities", "" },
        { "POST", "/api/v1/admin/cities", "city" },
        { "PUT", "/api/v1/admin/cities/cidade-exemplo", "city-update" },
        { "POST", "/api/v1/admin/styles", "style" },
        { "PUT", "/api/v1/admin/styles/doom", "style" },
    };

    /// <summary>As mutações de admin do §6.</summary>
    public static readonly TheoryData<string, string, string> Mutations = new()
    {
        { "POST", "/api/v1/admin/cities", "city" },
        { "PUT", "/api/v1/admin/cities/cidade-exemplo", "city-update" },
        { "POST", "/api/v1/admin/styles", "style" },
        { "PUT", "/api/v1/admin/styles/doom", "style" },
    };

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-04")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Endpoints))]
    public async Task AnonymousGets401AuthRequired(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var client = api.CreateAdminClient();

        using var response = await client.SendAsync(
            new HttpMethod(method), route, ct, Body(body), method == "GET" ? null : await client.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-04")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Endpoints))]
    public async Task FanGets403AuthForbidden(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.FanEmail, ct);

        using var response = await client.SendAsync(
            new HttpMethod(method), route, ct, Body(body), method == "GET" ? null : await client.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-05")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Mutations))]
    public async Task AdminWithoutCsrfGets400AndNothingChanges(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);
        var citiesBefore = await SnapshotAsync(client, "/api/v1/admin/cities", ct);
        var stylesBefore = await SnapshotAsync(client, "/api/v1/styles", ct);

        using var response = await client.SendAsync(new HttpMethod(method), route, ct, Body(body), csrfToken: null);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        (await SnapshotAsync(client, "/api/v1/admin/cities", ct)).ShouldBe(citiesBefore);
        (await SnapshotAsync(client, "/api/v1/styles", ct)).ShouldBe(stylesBefore);
    }

    private async Task<AdminApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    private static object? Body(string kind) => kind switch
    {
        "city" => CityBody,
        "city-update" => CityUpdate,
        "style" => StyleBody,
        _ => null,
    };

    /// <summary>O corpo de um GET que precisa dar 200 (estado antes e depois da mutação recusada).</summary>
    private static async Task<string> SnapshotAsync(AdminApiClient client, string route, CancellationToken ct)
    {
        using var response = await client.GetAsync(route, ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).ToString();
    }
}
