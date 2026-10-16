using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0010 CT-01 — contrato v1 da agenda (<c>GET /api/v1/cities/{slug}/agenda</c>): com a política de fallback
/// ativa (um endpoint sem declaração responde 401 a anônimos), a agenda continua pública — 200 sem login, com o
/// mesmo schema (<c>city</c> {slug, name, state, timeZone} e <c>shows</c>), porque o endpoint declara
/// <c>AllowAnonymous</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AgendaUnderFallbackPolicyTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0010:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task AgendaStaysAnonymousWithTheSameSchemaUnderTheFallbackPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await postgres.ResetAsync("events");
        // Development: migrações e seed da cidade de exemplo no start (como o Compose local).
        await using var factory = new SecurityApiFactory("Development", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();

        using var fallback = await client.GetAsync(SecurityTestEndpoints.Fallback, ct);
        fallback.StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized,
            "a política de fallback (usuário autenticado) não está ativa: o contrato seria verificado sem ela");

        using var response = await client.GetAsync("/api/v1/cities/cidade-exemplo/agenda", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["city", "shows"], ignoreOrder: true);
        var city = document.RootElement.GetProperty("city");
        city.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["slug", "name", "state", "timeZone"], ignoreOrder: true);
        city.GetProperty("slug").GetString().ShouldBe("cidade-exemplo");
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Sao_Paulo");
        document.RootElement.GetProperty("shows").ValueKind.ShouldBe(JsonValueKind.Array);
    }
}
