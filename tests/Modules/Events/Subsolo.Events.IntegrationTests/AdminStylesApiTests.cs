using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0017 IT-01 — com o PostgreSQL real migrado do zero, <c>GET /api/v1/styles</c> (público) traz os 5 estilos
/// iniciais da migração de dados em ordem de nome; como admin, <c>POST /api/v1/admin/styles</c> com "Crust" dá 201 e o
/// estilo aparece na lista; repetir dá 409 <c>events.style_taken</c>; renomear para "Crust Punk" mantém o slug
/// <c>crust</c>; renomear para "Doom" dá 409; renomear um slug inexistente dá 404 <c>events.style_not_found</c>; nome
/// com 1 ou 31 caracteres dá 400 <c>events.invalid_style</c>. Emenda 2 — vários POST simultâneos com o mesmo nome: um
/// 201 e o resto 409 <c>events.style_taken</c>, nunca 500.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdminStylesApiTests(PostgresFixture postgres)
{
    private const string Styles = "/api/v1/styles";
    private const string AdminStyles = "/api/v1/admin/styles";

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task FreshDatabaseHasTheFiveInitialStylesOrderedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new AdminApiFactory(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateAdminClient();

        using var response = await client.GetAsync(Styles, ct);

        var styles = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        styles.ValueKind.ShouldBe(JsonValueKind.Array, styles.ToString());
        var listed = StylesOf(styles);
        listed.Select(style => style.Slug).ShouldBe(["black", "death", "doom", "grind", "thrash"], styles.ToString());
        foreach (var (slug, name) in listed)
        {
            name.ShouldNotBeNull(styles.ToString());
            name.ToLowerInvariant().ShouldBe(slug, $"o nome do estilo inicial {slug} é o próprio slug");
        }

        foreach (var style in styles.EnumerateArray())
        {
            string.Join(",", style.EnumerateObject().Select(property => property.Name).Order()).ShouldBe("name,slug", styles.ToString());
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AdminCreatesAStyleThatShowsUpInTheListAndCannotRepeatIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new AdminApiFactory(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var created = await client.MutateAsync(HttpMethod.Post, AdminStyles, new { name = "Crust" }, ct);

        var style = await created.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        style.GetProperty("slug").GetString().ShouldBe("crust");
        style.GetProperty("name").GetString().ShouldBe("Crust");

        using var list = await client.GetAsync(Styles, ct);
        var styles = await list.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        StylesOf(styles).ShouldContain(("crust", "Crust"), styles.ToString());
        StylesOf(styles).Select(item => item.Slug).ShouldBe(["black", "crust", "death", "doom", "grind", "thrash"], styles.ToString());

        using var repeated = await client.MutateAsync(HttpMethod.Post, AdminStyles, new { name = "Crust" }, ct);
        await repeated.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.style_taken", ct);

        // "Black" gera o slug do estilo inicial black.
        using var sameSlug = await client.MutateAsync(HttpMethod.Post, AdminStyles, new { name = "Black" }, ct);
        await sameSlug.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.style_taken", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-01")]
    [Trait("Kind", "Integration")]
    [InlineData(1)]
    [InlineData(31)]
    public async Task StyleNameOutside2To30CharactersIsRejected(int length)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new AdminApiFactory(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var created = await client.MutateAsync(HttpMethod.Post, AdminStyles, new { name = new string('x', length) }, ct);
        (await created.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_style", ct)).ShouldHaveFieldError("name");

        using var renamed = await client.MutateAsync(HttpMethod.Put, $"{AdminStyles}/doom", new { name = new string('x', length) }, ct);
        (await renamed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_style", ct)).ShouldHaveFieldError("name");
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task RenamingKeepsTheSlugAndRejectsAnotherStylesNameOrAnUnknownSlug()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new AdminApiFactory(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);
        using (var created = await client.MutateAsync(HttpMethod.Post, AdminStyles, new { name = "Crust" }, ct))
        {
            await created.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        }

        using var renamed = await client.MutateAsync(HttpMethod.Put, $"{AdminStyles}/crust", new { name = "Crust Punk" }, ct);

        var style = await renamed.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        style.GetProperty("slug").GetString().ShouldBe("crust");
        style.GetProperty("name").GetString().ShouldBe("Crust Punk");
        using (var list = await client.GetAsync(Styles, ct))
        {
            var styles = await list.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            StylesOf(styles).ShouldContain(("crust", "Crust Punk"), styles.ToString());
            StylesOf(styles).ShouldNotContain(item => item.Slug == "crust-punk", styles.ToString());
        }

        using var taken = await client.MutateAsync(HttpMethod.Put, $"{AdminStyles}/crust", new { name = "Doom" }, ct);
        await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.style_taken", ct);

        using var missing = await client.MutateAsync(HttpMethod.Put, $"{AdminStyles}/nao-existe", new { name = "Sludge" }, ct);
        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.style_not_found", ct);

        using (var list = await client.GetAsync(Styles, ct))
        {
            var styles = await list.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            StylesOf(styles).ShouldContain(("crust", "Crust Punk"), "o 409 não pode ter mudado o estilo");
            StylesOf(styles).Count.ShouldBe(6, styles.ToString());
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ConcurrentCreatesWithTheSameNameGiveOne201AndThe409sNever500()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new AdminApiFactory(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var admins = await ConcurrentCreates.AdminsAsync(api, ct);
        try
        {
            for (var round = 0; round < ConcurrentCreates.Rounds; round++)
            {
                await ConcurrentCreates.ShouldCreateOnceAsync(
                    admins, AdminStyles, new { name = $"Crust {round}" }, "events.style_taken", ct);
            }

            using var list = await admins[0].Client.GetAsync(Styles, ct);
            var styles = await list.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            for (var round = 0; round < ConcurrentCreates.Rounds; round++)
            {
                StylesOf(styles).Count(style => style.Slug == $"crust-{round}").ShouldBe(1, styles.ToString());
            }
        }
        finally
        {
            foreach (var (client, _) in admins)
            {
                client.Dispose();
            }
        }
    }

    private static List<(string? Slug, string? Name)> StylesOf(JsonElement styles) =>
        [.. styles.EnumerateArray().Select(style => (style.GetProperty("slug").GetString(), style.GetProperty("name").GetString()))];
}
