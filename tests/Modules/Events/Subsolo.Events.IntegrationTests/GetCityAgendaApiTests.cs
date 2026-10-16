using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0008 IT-01..IT-03 — <c>GET /api/v1/cities/{slug}/agenda</c> no Subsolo.Api com PostgreSQL real: a cidade
/// seed com <c>shows: []</c> (200), slug inválido (400 <c>agenda.invalid_slug</c>) e cidade não cadastrada (404
/// <c>agenda.city_not_found</c>), sempre em ProblemDetails com o código em <c>code</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GetCityAgendaApiTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0008:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task SeededCityReturns200WithTheCityAndNoShows()
    {
        var ct = TestContext.Current.CancellationToken;
        await postgres.ResetAsync("events");
        await using var factory = new EventsApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/cities/cidade-exemplo/agenda", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        using var document = JsonDocument.Parse(body);
        var city = document.RootElement.GetProperty("city");
        city.GetProperty("slug").GetString().ShouldBe("cidade-exemplo");
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        (city.GetProperty("state").GetString() ?? string.Empty).ShouldMatch("^[A-Z]{2}$");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Sao_Paulo");
        var shows = document.RootElement.GetProperty("shows");
        shows.ValueKind.ShouldBe(JsonValueKind.Array);
        shows.GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [Trait("Spec", "SPEC-0008:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("Cidade_X")]
    [InlineData("cidade-EXEMPLO")]
    public async Task InvalidSlugReturns400ProblemDetailsWithInvalidSlugCode(string slug)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new EventsApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/cities/{slug}/agenda", ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "agenda.invalid_slug", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task UnknownSlugReturns404ProblemDetailsWithCityNotFoundCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new EventsApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/cities/cidade-inexistente/agenda", ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "agenda.city_not_found", ct);
    }

    private static async Task ShouldBeProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(status, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status, body);
        document.RootElement.TryGetProperty("code", out var actual).ShouldBeTrue($"ProblemDetails sem code: {body}");
        actual.GetString().ShouldBe(code, body);
    }
}
