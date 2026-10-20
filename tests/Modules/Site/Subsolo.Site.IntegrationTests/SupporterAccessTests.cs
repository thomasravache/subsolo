using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Site.IntegrationTests.Infrastructure;

namespace Subsolo.Site.IntegrationTests;

/// <summary>
/// SPEC-0028 IT-02 — nas rotas de admin do §6 (<c>POST /api/v1/admin/supporters</c> e
/// <c>DELETE /api/v1/admin/supporters/{id}</c>): anônimo → 401 <c>auth.required</c>; fã logado → 403
/// <c>auth.forbidden</c>; admin → 201 <c>{id}</c> e 204; dados inválidos (nome de 1 ou 61 caracteres, link
/// <c>http</c>) → 400 <c>site.invalid_supporter</c>; remover inexistente → 404 <c>site.supporter_not_found</c>; sem
/// <c>X-CSRF</c> (ou com token inválido) → 400 <c>security.csrf_invalid</c>. Quem é recusado não muda a lista.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SupporterAccessTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.apoiadores@subsolo.test";

    public static TheoryData<string> Methods() => new() { "POST", "DELETE" };

    private static (string Route, object? Body) Request(string method, Guid existing) =>
        method == "POST"
            ? (SiteRequests.AdminSupporters, SiteRequests.Body("Abismo Records", "https://abismo.example"))
            : (SiteRequests.AdminSupporter(existing), null);

    [Theory]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Methods))]
    public async Task AnonymousIs401AndChangesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();
        var existing = await admin.AddSupporterAsync("Putridearth Zine", null, ct);
        var before = await anonymous.SupportersAsync(ct);
        var (route, body) = Request(method, existing);

        using var response = await anonymous.MutateAsync(new HttpMethod(method), route, body, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        (await anonymous.SupportersAsync(ct)).ShouldBe(before);
    }

    [Theory]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Methods))]
    public async Task FanIs403AndChangesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var fan = await api.SignedInAsync(FanEmail, ct);
        using var anonymous = api.CreateSiteClient();
        var existing = await admin.AddSupporterAsync("Putridearth Zine", null, ct);
        var before = await anonymous.SupportersAsync(ct);
        var (route, body) = Request(method, existing);

        using var response = await fan.MutateAsync(new HttpMethod(method), route, body, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        (await anonymous.SupportersAsync(ct)).ShouldBe(before);
    }

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AdminAddsWith201AndRemovesWith204()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();

        var id = await admin.AddSupporterAsync("Putridearth Zine", "https://zine.example", ct);
        (await anonymous.SupportersAsync(ct)).ShouldBe([new SupporterItem(id, "Putridearth Zine", "https://zine.example")]);

        using (var response = await admin.DeleteAsync(SiteRequests.AdminSupporter(id), ct))
        {
            await response.ShouldBeNoContentAsync(ct);
        }

        (await anonymous.SupportersAsync(ct)).ShouldBeEmpty();
    }

    [Theory]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(1, "https://zine.example", "name")]
    [InlineData(61, "https://zine.example", "name")]
    [InlineData(16, "http://x", "url")]
    public async Task InvalidSupporterIs400AndSavesNothing(int nameLength, string url, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();

        using var response = await admin.PostAsync(
            SiteRequests.AdminSupporters, SiteRequests.Body(new string('a', nameLength), url), ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "site.invalid_supporter", ct);
        problem.ShouldHaveFieldError(field);
        (await anonymous.SupportersAsync(ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task RemovingAMissingSupporterIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);

        using var response = await admin.DeleteAsync(SiteRequests.AdminSupporter(Guid.CreateVersion7()), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "site.supporter_not_found", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Methods))]
    public async Task AdminWithoutCsrfIs400AndChangesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();
        var existing = await admin.AddSupporterAsync("Putridearth Zine", null, ct);
        var before = await anonymous.SupportersAsync(ct);
        var (route, body) = Request(method, existing);

        using (var withoutToken = await admin.SendAsync(new HttpMethod(method), route, ct, body))
        {
            await withoutToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        using (var invalidToken = await admin.SendAsync(new HttpMethod(method), route, ct, body, csrfToken: "token-invalido"))
        {
            await invalidToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await anonymous.SupportersAsync(ct)).ShouldBe(before);
    }
}
