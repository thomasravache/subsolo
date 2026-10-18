using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0017 IT-02 — o admin cria "São José dos Pinhais"/PR/America/Sao_Paulo sem slug: 201 com o slug gerado
/// <c>sao-jose-dos-pinhais</c> (Location <c>/api/v1/admin/cities/sao-jose-dos-pinhais</c>); a cidade entra em
/// <c>GET /api/v1/cities</c> e em <c>GET /api/v1/admin/cities</c>, e <c>/api/v1/cities/sao-jose-dos-pinhais/agenda</c>
/// responde 200; dados inválidos dão 400 <c>events.invalid_city</c> com o campo; slug reservado, 400
/// <c>events.city_slug_reserved</c>; slug repetido, 409 <c>events.city_slug_taken</c>.
/// IT-03 — PUT com nome, UF e fuso novos dá 200 e o slug continua o mesmo; PUT num slug inexistente dá 404
/// <c>events.city_not_found</c>; PUT com dados inválidos dá 400 <c>events.invalid_city</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdminCitiesApiTests(PostgresFixture postgres)
{
    private const string AdminCities = "/api/v1/admin/cities";

    /// <summary>Cidade do seed de desenvolvimento (SPEC-0008).</summary>
    private const string SeededSlug = "cidade-exemplo";

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AdminCreatesACityWithTheGeneratedSlugAndItGetsAnAgenda()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var created = await client.MutateAsync(
            HttpMethod.Post,
            AdminCities,
            new { name = "São José dos Pinhais", state = "PR", timeZone = "America/Sao_Paulo", slug = (string?)null },
            ct);

        var city = await created.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        city.GetProperty("slug").GetString().ShouldBe("sao-jose-dos-pinhais");
        city.GetProperty("name").GetString().ShouldBe("São José dos Pinhais");
        city.GetProperty("state").GetString().ShouldBe("PR");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Sao_Paulo");
        created.Headers.Location?.OriginalString.ShouldEndWith($"{AdminCities}/sao-jose-dos-pinhais");

        using (var publicList = await client.GetAsync("/api/v1/cities", ct))
        {
            var cities = await publicList.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            cities.EnumerateArray().Select(item => item.GetProperty("slug").GetString())
                .ShouldContain("sao-jose-dos-pinhais", cities.ToString());
        }

        using (var adminList = await client.GetAsync(AdminCities, ct))
        {
            var cities = await adminList.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            var listed = cities.EnumerateArray().Select(item => (
                Slug: item.GetProperty("slug").GetString(),
                Name: item.GetProperty("name").GetString(),
                State: item.GetProperty("state").GetString(),
                TimeZone: item.GetProperty("timeZone").GetString())).ToList();
            listed.ShouldContain(("sao-jose-dos-pinhais", "São José dos Pinhais", "PR", "America/Sao_Paulo"), cities.ToString());
            listed.Select(item => item.Name).ShouldBe(listed.Select(item => item.Name).Order(StringComparer.Ordinal), "ordenadas pelo nome");
        }

        using var agenda = await client.GetAsync("/api/v1/cities/sao-jose-dos-pinhais/agenda", ct);
        var body = await agenda.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.GetProperty("city").GetProperty("slug").GetString().ShouldBe("sao-jose-dos-pinhais");
        body.GetProperty("shows").GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("A", "PR", "America/Sao_Paulo", null, "name")]
    [InlineData("Cidade Nova", "XX", "America/Sao_Paulo", null, "state")]
    [InlineData("Cidade Nova", "PR", "Mars/Olympus", null, "timeZone")]
    [InlineData("Cidade Nova", "PR", "America/Sao_Paulo", "Cidade_X", "slug")]
    public async Task InvalidCityDataIsRejectedWithTheField(string name, string state, string timeZone, string? slug, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var response = await client.MutateAsync(HttpMethod.Post, AdminCities, new { name, state, timeZone, slug }, ct);

        (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_city", ct)).ShouldHaveFieldError(field);
        (await SlugsAsync(client, ct)).ShouldNotContain("cidade-nova");
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("admin")]
    [InlineData("entrar")]
    public async Task ReservedSlugIsRejected(string slug)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var response = await client.MutateAsync(
            HttpMethod.Post, AdminCities, new { name = "Cidade Nova", state = "PR", timeZone = "America/Sao_Paulo", slug }, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.city_slug_reserved", ct);
        (await SlugsAsync(client, ct)).ShouldNotContain(slug);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task RepeatedSlugIsRejectedWithConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);
        var body = new { name = "São José dos Pinhais", state = "PR", timeZone = "America/Sao_Paulo", slug = (string?)null };
        using (var first = await client.MutateAsync(HttpMethod.Post, AdminCities, body, ct))
        {
            await first.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        }

        using var generated = await client.MutateAsync(HttpMethod.Post, AdminCities, body, ct);
        await generated.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.city_slug_taken", ct);

        using var explicitSlug = await client.MutateAsync(
            HttpMethod.Post, AdminCities, new { name = "Outra Exemplo", state = "SP", timeZone = "America/Sao_Paulo", slug = SeededSlug }, ct);
        await explicitSlug.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.city_slug_taken", ct);

        (await SlugsAsync(client, ct)).Count(slug => slug == "sao-jose-dos-pinhais").ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AdminUpdatesACityAndTheSlugStaysTheSame()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var updated = await client.MutateAsync(
            HttpMethod.Put,
            $"{AdminCities}/{SeededSlug}",
            new { name = "Cidade Exemplo Renomeada", state = "RJ", timeZone = "America/Manaus" },
            ct);

        var city = await updated.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        city.GetProperty("slug").GetString().ShouldBe(SeededSlug);
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo Renomeada");
        city.GetProperty("state").GetString().ShouldBe("RJ");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Manaus");

        using var agenda = await client.GetAsync($"/api/v1/cities/{SeededSlug}/agenda", ct);
        var body = await agenda.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.GetProperty("city").GetProperty("name").GetString().ShouldBe("Cidade Exemplo Renomeada");
        body.GetProperty("city").GetProperty("timeZone").GetString().ShouldBe("America/Manaus");
        (await SlugsAsync(client, ct)).ShouldNotContain("cidade-exemplo-renomeada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task UpdatingAnUnknownSlugReturns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var response = await client.MutateAsync(
            HttpMethod.Put, $"{AdminCities}/nao-existe", new { name = "Não Existe", state = "SP", timeZone = "America/Sao_Paulo" }, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.city_not_found", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task UpdatingWithInvalidDataReturns400AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await AdminAsync(ct);
        using var client = api.CreateAdminClient();
        await client.SignInAsync(AdminApiFactory.AdminEmail, ct);

        using var response = await client.MutateAsync(
            HttpMethod.Put, $"{AdminCities}/{SeededSlug}", new { name = "Cidade Exemplo", state = "XX", timeZone = "America/Sao_Paulo" }, ct);

        (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_city", ct)).ShouldHaveFieldError("state");
        using var agenda = await client.GetAsync($"/api/v1/cities/{SeededSlug}/agenda", ct);
        var body = await agenda.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.GetProperty("city").GetProperty("state").GetString().ShouldBe("SP");
    }

    private async Task<AdminApiFactory> AdminAsync(CancellationToken ct) =>
        new(await AdminApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    /// <summary>Os slugs de <c>GET /api/v1/admin/cities</c>.</summary>
    private static async Task<List<string?>> SlugsAsync(AdminApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(AdminCities, ct);
        var cities = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        cities.ValueKind.ShouldBe(JsonValueKind.Array, cities.ToString());
        return [.. cities.EnumerateArray().Select(city => city.GetProperty("slug").GetString())];
    }
}
