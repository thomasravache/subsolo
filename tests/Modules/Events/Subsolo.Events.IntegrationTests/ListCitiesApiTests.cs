using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0011 IT-02 — <c>GET /api/v1/cities</c> no Subsolo.Api com PostgreSQL real: público (sem autenticação),
/// devolve 200 com as cidades seed de desenvolvimento como <c>[{ slug, name }]</c>, ordenadas por nome. É a lista
/// do seletor de cidade do cabeçalho do app.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ListCitiesApiTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0011:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ListsTheSeededCitiesAsSlugAndNameOrderedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        await postgres.ResetAsync("events");
        await using var factory = new EventsApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/cities", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array, body);
        var cities = document.RootElement.EnumerateArray()
            .Select(city => (
                Slug: city.GetProperty("slug").GetString(),
                Name: city.GetProperty("name").GetString()))
            .ToList();
        cities.ShouldBe(
        [
            ("cidade-exemplo", "Cidade Exemplo"),
            ("outra-cidade", "Outra Cidade"),
        ]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0011:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task EachCityHasOnlySlugAndName()
    {
        var ct = TestContext.Current.CancellationToken;
        await postgres.ResetAsync("events");
        await using var factory = new EventsApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/cities", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array, body);
        document.RootElement.GetArrayLength().ShouldBeGreaterThan(0, body);
        foreach (var city in document.RootElement.EnumerateArray())
        {
            string.Join(",", city.EnumerateObject().Select(property => property.Name).Order()).ShouldBe("name,slug", body);
        }
    }
}
