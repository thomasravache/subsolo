using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Observability.IntegrationTests.Infrastructure;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// SPEC-0009 CT-01 (contrato v1 da agenda) — com a instrumentação ativa (exportação configurada e spans exportados),
/// <c>GET /api/v1/cities/{slug}/agenda</c> responde com o mesmo status, o mesmo tipo de conteúdo e o mesmo schema
/// (nomes e tipos dos campos) que a API sem exportação: cidade seed (200), cidade não cadastrada (404) e slug fora do
/// padrão (400). A rota <c>/{cidade}</c> do web é coberta no projeto do AppHost.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AgendaContractUnderInstrumentationTests(PostgresFixture postgres)
{
    [Theory]
    [Trait("Spec", "SPEC-0009:CT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("cidade-exemplo", HttpStatusCode.OK)]
    [InlineData("cidade-inexistente", HttpStatusCode.NotFound)]
    [InlineData("Cidade_X", HttpStatusCode.BadRequest)]
    public async Task AgendaResponseIsUnchangedByInstrumentation(string slug, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await ObservedApi.DatabaseAsync(postgres, ct);
        await using var plain = new ObservedApiFactory(database);
        await using var instrumented = new ObservedApiFactory(database, ObservedApiFactory.DeadEndpoint());
        var path = new Uri($"/api/v1/cities/{slug}/agenda", UriKind.Relative);

        var reference = await GetAsync(plain, path, ct);
        var observed = await GetAsync(instrumented, path, ct);

        instrumented.Spans.ShouldContain(
            span => span.Kind == ActivityKind.Server && span.DisplayName.Contains("agenda", StringComparison.OrdinalIgnoreCase),
            "instrumentação inativa: nenhum span de servidor da agenda exportado");
        reference.Status.ShouldBe(expected, "a referência sem exportação não segue o contrato");
        observed.Status.ShouldBe(reference.Status);
        observed.ContentType.ShouldBe(reference.ContentType);
        observed.Schema.ShouldBe(reference.Schema);
    }

    private static async Task<Snapshot> GetAsync(ObservedApiFactory api, Uri path, CancellationToken ct)
    {
        using var client = api.CreateClient();
        using var response = await client.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);
        return new Snapshot(
            response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            string.Join("\n", Shape(document.RootElement, "$").Order(StringComparer.Ordinal)));
    }

    /// <summary>Caminho e tipo JSON de cada campo (valores fora: traceId e datas variam entre execuções).</summary>
    private static IEnumerable<string> Shape(JsonElement element, string path)
    {
        yield return $"{path}:{element.ValueKind}";
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var line in Shape(property.Value, $"{path}.{property.Name}"))
                    {
                        yield return line;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var line in element.EnumerateArray().SelectMany(item => Shape(item, $"{path}[]")).Distinct())
                {
                    yield return line;
                }

                break;
        }
    }

    private sealed record Snapshot(HttpStatusCode Status, string? ContentType, string Schema);
}
