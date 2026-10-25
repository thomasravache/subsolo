using System.Net;
using System.Text.Json;
using Subsolo.Api.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0039 UT-01 (H37-01) — o <c>AllowedValuesSchemaTransformer</c> preserva o <c>null</c> da propriedade anulável:
/// <c>string?</c> com <c>[AllowedValues("a", "b")]</c> sai com o tipo <c>string | null</c> e o <c>enum</c> <c>a</c>,
/// <c>b</c> e <c>null</c>; <c>string</c> não anulável sai só <c>string</c>, com o <c>enum</c> só <c>a</c> e <c>b</c>.
/// O <c>reason</c> do <c>ReportShowRequest</c> continua <c>string</c> com os quatro motivos e sem nulo (o snapshot não
/// muda).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AllowedValuesSchemaTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0039:UT-01")]
    [Trait("Kind", "Unit")]
    public async Task ANullablePropertyKeepsNullInTypeAndEnumAndANonNullableOneDoesNot()
    {
        var ct = TestContext.Current.CancellationToken;
        var schemas = await SchemasAsync(ct);

        var probe = schemas.GetProperty(nameof(AllowedValuesProbeRequest)).GetProperty("properties");

        var nullable = probe.GetProperty("nullableChoice");
        Types(nullable).ShouldBe(["null", "string"], ignoreOrder: true, nullable.ToString());
        EnumValues(nullable).ShouldBe(["a", "b", null], ignoreOrder: true, nullable.ToString());

        var required = probe.GetProperty("requiredChoice");
        Types(required).ShouldBe(["string"], ignoreOrder: true, required.ToString());
        EnumValues(required).ShouldBe(["a", "b"], ignoreOrder: true, required.ToString());
    }

    [Fact]
    [Trait("Spec", "SPEC-0039:UT-01")]
    [Trait("Kind", "Unit")]
    public async Task TheReportReasonStaysAStringEnumWithoutNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var schemas = await SchemasAsync(ct);

        var reason = schemas.GetProperty("ReportShowRequest").GetProperty("properties").GetProperty("reason");

        Types(reason).ShouldBe(["string"], ignoreOrder: true, reason.ToString());
        EnumValues(reason).ShouldBe(["fake", "cancelled_unannounced", "wrong_info", "other"], ignoreOrder: true, reason.ToString());
    }

    private async Task<JsonElement> SchemasAsync(CancellationToken ct)
    {
        await using var factory = new ApiFactory("Development", postgres.ConnectionString, withTestEndpoints: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.GetProperty("components").GetProperty("schemas").Clone();
    }

    /// <summary>O <c>type</c> do esquema como lista (texto único ou vetor).</summary>
    private static string[] Types(JsonElement schema)
    {
        schema.TryGetProperty("type", out var type).ShouldBeTrue($"esquema sem type: {schema}");
        return type.ValueKind == JsonValueKind.Array
            ? [.. type.EnumerateArray().Select(item => item.GetString()!)]
            : [type.GetString()!];
    }

    /// <summary>Os valores do <c>enum</c> do esquema (<c>null</c> do JSON vira <see langword="null"/>).</summary>
    private static string?[] EnumValues(JsonElement schema)
    {
        schema.TryGetProperty("enum", out var values).ShouldBeTrue($"esquema sem enum: {schema}");
        return [.. values.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null ? null : item.GetString())];
    }
}
