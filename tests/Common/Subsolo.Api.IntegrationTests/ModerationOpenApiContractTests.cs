using System.Net;
using System.Text.Json;
using Subsolo.Api.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0033 CT-01 (OpenAPI gerado) — o <c>GET /openapi/v1.json</c> da API publica as rotas do §6 com os status e os
/// códigos de erro (<c>code=…</c> na descrição): <c>POST /api/v1/shows/{id}/reports</c> (201, 400, 404, 409, 429),
/// <c>GET /api/v1/admin/reports</c> (200, 401, 403), <c>POST …/{showId}/keep</c> e <c>…/hide</c> (204, 404); o item de
/// "Meus shows" (<c>MyShowResponse</c>) ganha <c>hidden</c> (booleano) e a edição e a mudança de situação do show declaram
/// 409 <c>events.show_hidden</c> (Emenda 1). A igualdade com o snapshot versionado é o SPEC-0007:CT-01; o snapshot e o
/// cliente TypeScript ficam no <c>web/src/api/reports-contract.test.ts</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ModerationOpenApiContractTests(PostgresFixture postgres)
{
    private const string ReportsPath = "/api/v1/shows/{id}/reports";
    private const string QueuePath = "/api/v1/admin/reports";
    private const string KeepPath = "/api/v1/admin/reports/{showId}/keep";
    private const string HidePath = "/api/v1/admin/reports/{showId}/hide";

    [Fact]
    [Trait("Spec", "SPEC-0033:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheGeneratedOpenApiHasTheModerationRoutesAndTheHiddenShowShapes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory("Development", postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var paths = document.RootElement.GetProperty("paths");

        var report = Operation(paths, ReportsPath, "post");
        ShouldDeclare(report, "201");
        ShouldDeclare(report, "400", "moderation.invalid_report", "security.csrf_invalid");
        ShouldDeclare(report, "401", "auth.required");
        ShouldDeclare(report, "404", "events.show_not_found");
        ShouldDeclare(report, "409", "moderation.already_reported");
        ShouldDeclare(report, "429", "security.rate_limited");

        var queue = Operation(paths, QueuePath, "get");
        ShouldDeclare(queue, "200");
        ShouldDeclare(queue, "401", "auth.required");
        ShouldDeclare(queue, "403", "auth.forbidden");

        foreach (var path in new[] { KeepPath, HidePath })
        {
            var decision = Operation(paths, path, "post");
            ShouldDeclare(decision, "204");
            ShouldDeclare(decision, "400", "security.csrf_invalid");
            ShouldDeclare(decision, "401", "auth.required");
            ShouldDeclare(decision, "403", "auth.forbidden");
            ShouldDeclare(decision, "404", "moderation.no_pending_reports");
        }

        ShouldDeclare(Operation(paths, "/api/v1/shows/{id}", "put"), "409", "events.show_hidden");
        ShouldDeclare(Operation(paths, "/api/v1/shows/{id}/status", "post"), "409", "events.show_hidden");

        var myShow = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("MyShowResponse");
        myShow.GetProperty("properties").TryGetProperty("hidden", out var hidden)
            .ShouldBeTrue($"MyShowResponse sem hidden: {myShow}");
        hidden.GetProperty("type").ToString().ShouldContain("boolean", Case.Sensitive, hidden.ToString());
    }

    private static JsonElement Operation(JsonElement paths, string path, string method)
    {
        paths.TryGetProperty(path, out var item).ShouldBeTrue($"{path} ausente do OpenAPI gerado");
        item.TryGetProperty(method, out var operation).ShouldBeTrue($"{method.ToUpperInvariant()} {path} ausente do OpenAPI gerado");
        return operation;
    }

    private static void ShouldDeclare(JsonElement operation, string status, params string[] codes)
    {
        operation.GetProperty("responses").TryGetProperty(status, out var response)
            .ShouldBeTrue($"status {status} não declarado: {operation.GetProperty("responses")}");
        var description = response.TryGetProperty("description", out var text) ? text.GetString() ?? string.Empty : string.Empty;
        foreach (var code in codes)
        {
            description.ShouldContain($"code={code}", Case.Sensitive, $"{status} sem o código {code}");
        }
    }
}
