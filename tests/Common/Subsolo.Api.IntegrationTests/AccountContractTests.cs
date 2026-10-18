using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0019 CT-01 — o snapshot <c>docs/api/openapi.v1.json</c> (igual ao documento gerado pela API, conferido pelo
/// CT-01 da SPEC-0007) publica os três endpoints da "Minha conta" com os corpos, status e códigos do §6:
/// <c>PUT /api/v1/me</c> (corpo <c>{displayName: string|null}</c>; 200 com o corpo do <c>GET /me</c>; 400
/// <c>users.invalid_display_name</c> e <c>security.csrf_invalid</c>; 401 <c>auth.required</c>; 429),
/// <c>GET /api/v1/me/export</c> (200 JSON <c>{exportedAtUtc, account, sessions, sections}</c>; 401; 429) e
/// <c>DELETE /api/v1/me</c> (corpo <c>{confirmEmail}</c>; 204 sem corpo; 400 <c>users.delete_confirmation_mismatch</c> e
/// <c>security.csrf_invalid</c>; 401; 429); o cliente TypeScript <c>web/src/api/schema.d.ts</c> foi regenerado com eles
/// (a igualdade exata com o snapshot é o <c>pnpm api:check</c>, CT-01 da SPEC-0008).
/// </summary>
public sealed partial class AccountContractTests
{
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";
    private const string ClientRelativePath = "web/src/api/schema.d.ts";

    [Fact]
    [Trait("Spec", "SPEC-0019:CT-01")]
    public async Task PutMeIsPublishedWithTheBodyTheMeResponseAndTheErrors()
    {
        var snapshot = await SnapshotAsync();
        var operation = Operation(snapshot, "/api/v1/me", "put");

        var body = RequestSchema(snapshot, operation, "PUT /api/v1/me");
        var properties = body["properties"].ShouldBeOfType<JsonObject>();
        properties.Select(property => property.Key).ShouldBe(["displayName"]);
        TypeOf(properties["displayName"]).ShouldContain("string");
        TypeOf(properties["displayName"]).ShouldContain("null", customMessage: "displayName é string|null (null remove o nome)");

        var responses = operation["responses"].ShouldBeOfType<JsonObject>();
        StatusesOf(responses).ShouldBe(["200", "400", "401", "429"], ignoreOrder: true, customMessage: "status do §6 (503 service.unavailable, do padrão das rotas com sessão, é opcional)");
        var ok = Resolve(snapshot, responses["200"]?["content"]?["application/json"]?["schema"])
            .ShouldBeOfType<JsonObject>("200 de PUT /api/v1/me sem schema JSON");
        ok["properties"].ShouldBeOfType<JsonObject>().Select(property => property.Key)
            .ShouldBe(["id", "email", "displayName", "roles"], ignoreOrder: true, "o 200 é o mesmo corpo do GET /me");

        ShouldBeProblem(responses, "400", "users.invalid_display_name", "security.csrf_invalid");
        ShouldBeProblem(responses, "401", "auth.required");
        ShouldBeProblem(responses, "429", "security.rate_limited");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:CT-01")]
    public async Task ExportIsPublishedWithTheFileShapeAndTheErrors()
    {
        var snapshot = await SnapshotAsync();
        var operation = Operation(snapshot, "/api/v1/me/export", "get");

        (operation["requestBody"]).ShouldBeNull("o export não tem corpo");
        var responses = operation["responses"].ShouldBeOfType<JsonObject>();
        StatusesOf(responses).ShouldBe(["200", "401", "429"], ignoreOrder: true, customMessage: "status do §6 (503 service.unavailable, do padrão das rotas com sessão, é opcional)");
        var file = Resolve(snapshot, responses["200"]?["content"]?["application/json"]?["schema"])
            .ShouldBeOfType<JsonObject>("200 do export sem schema JSON");
        var properties = file["properties"].ShouldBeOfType<JsonObject>();
        properties.Select(property => property.Key)
            .ShouldBe(["exportedAtUtc", "account", "sessions", "sections"], ignoreOrder: true);

        var account = Resolve(snapshot, properties["account"]).ShouldBeOfType<JsonObject>("account sem schema");
        account["properties"].ShouldBeOfType<JsonObject>().Select(property => property.Key)
            .ShouldBe(["id", "email", "displayName", "roles", "createdAtUtc"], ignoreOrder: true);

        var sessions = Resolve(snapshot, properties["sessions"]).ShouldBeOfType<JsonObject>("sessions sem schema");
        TypeOf(sessions).ShouldContain("array");
        var session = Resolve(snapshot, sessions["items"]).ShouldBeOfType<JsonObject>("item de sessions sem schema");
        session["properties"].ShouldBeOfType<JsonObject>().Select(property => property.Key)
            .ShouldBe(
                ["createdAtUtc", "method", "lastSeenAtUtc", "expiresAtUtc", "absoluteExpiresAtUtc", "revokedAtUtc"],
                ignoreOrder: true,
                "a sessão exportada nunca leva hash nem token");

        TypeOf(Resolve(snapshot, properties["sections"])).ShouldContain("object", customMessage: "sections é um objeto (seção por nome)");

        ShouldBeProblem(responses, "401", "auth.required");
        ShouldBeProblem(responses, "429", "security.rate_limited");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:CT-01")]
    public async Task DeleteMeIsPublishedWithTheConfirmationAndTheErrors()
    {
        var snapshot = await SnapshotAsync();
        var operation = Operation(snapshot, "/api/v1/me", "delete");

        var body = RequestSchema(snapshot, operation, "DELETE /api/v1/me");
        var properties = body["properties"].ShouldBeOfType<JsonObject>();
        properties.Select(property => property.Key).ShouldBe(["confirmEmail"]);
        TypeOf(properties["confirmEmail"]).ShouldContain("string");

        var responses = operation["responses"].ShouldBeOfType<JsonObject>();
        StatusesOf(responses).ShouldBe(["204", "400", "401", "429"], ignoreOrder: true, customMessage: "status do §6 (503 service.unavailable, do padrão das rotas com sessão, é opcional)");
        (responses["204"]?["content"]).ShouldBeNull("204 não tem corpo");

        ShouldBeProblem(responses, "400", "users.delete_confirmation_mismatch", "security.csrf_invalid");
        ShouldBeProblem(responses, "401", "auth.required");
        ShouldBeProblem(responses, "429", "security.rate_limited");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:CT-01")]
    public async Task TypeScriptClientWasRegeneratedWithTheAccountEndpoints()
    {
        var client = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), ClientRelativePath), TestContext.Current.CancellationToken);
        const string Regenerate = "rode (cd web && pnpm api:generate)";

        client.ShouldContain("\"/api/v1/me/export\"", Case.Sensitive, $"{ClientRelativePath} sem /api/v1/me/export: {Regenerate}");
        var me = MePathBlock().Match(client);
        me.Success.ShouldBeTrue($"{ClientRelativePath} sem o bloco de /api/v1/me");
        me.Value.ShouldMatch(@"\bput: operations\[", $"{ClientRelativePath} sem PUT /api/v1/me: {Regenerate}");
        me.Value.ShouldMatch(@"\bdelete: operations\[", $"{ClientRelativePath} sem DELETE /api/v1/me: {Regenerate}");
    }

    /// <summary>O bloco <c>"/api/v1/me": { … };</c> de <c>paths</c> no cliente gerado (até o próximo caminho).</summary>
    [GeneratedRegex("\"/api/v1/me\": \\{.*?\\n  \\};", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MePathBlock();

    /// <summary>Os status documentados, sem o 503 <c>service.unavailable</c> que as rotas com sessão herdam (SPEC-0016 CT-03).</summary>
    private static IReadOnlyList<string> StatusesOf(JsonObject responses) =>
        [.. responses.Select(response => response.Key).Where(status => status != "503")];

    private static JsonObject RequestSchema(JsonObject snapshot, JsonObject operation, string name) =>
        Resolve(snapshot, operation["requestBody"]?["content"]?["application/json"]?["schema"])
            .ShouldBeOfType<JsonObject>($"{name} sem corpo JSON documentado");

    private static void ShouldBeProblem(JsonObject responses, string status, params string[] codes)
    {
        var response = responses[status].ShouldNotBeNull($"resposta {status} ausente");
        (response["content"]?["application/problem+json"]?["schema"]).ShouldNotBeNull($"{status} sem ProblemDetails");
        foreach (var code in codes)
        {
            response.ToJsonString().ShouldContain(code, Case.Sensitive, $"a resposta {status} não documenta o código {code}");
        }
    }

    private static JsonObject Operation(JsonObject snapshot, string path, string method)
    {
        var paths = snapshot["paths"].ShouldBeOfType<JsonObject>();
        paths.ContainsKey(path).ShouldBeTrue($"{SnapshotRelativePath} sem {path}; caminhos: {string.Join(", ", paths.Select(p => p.Key))}");
        return (paths[path]?[method]).ShouldBeOfType<JsonObject>($"{SnapshotRelativePath} sem {method.ToUpperInvariant()} {path}");
    }

    /// <summary>Segue um <c>$ref</c> local (<c>#/components/schemas/X</c>), inclusive dentro de <c>oneOf</c>/<c>allOf</c> de um item só.</summary>
    private static JsonNode? Resolve(JsonObject document, JsonNode? schema)
    {
        var reference = schema?["$ref"]?.GetValue<string>();
        if (reference is null)
        {
            var single = (schema?["allOf"] ?? schema?["oneOf"]) as JsonArray;
            return single is { Count: 1 } ? Resolve(document, single[0]) : schema;
        }

        JsonNode? node = document;
        foreach (var part in reference.TrimStart('#', '/').Split('/'))
        {
            node = node?[part];
        }

        return Resolve(document, node);
    }

    /// <summary>Os tipos de um schema (<c>type</c> como texto ou lista, OpenAPI 3.1).</summary>
    private static IReadOnlyList<string> TypeOf(JsonNode? schema) =>
        schema?["type"] switch
        {
            JsonArray types => [.. types.Select(type => type?.GetValue<string>() ?? string.Empty)],
            JsonValue type => [type.GetValue<string>()],
            _ => [],
        };

    private static async Task<JsonObject> SnapshotAsync()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), SnapshotRelativePath), TestContext.Current.CancellationToken);
        return JsonNode.Parse(json).ShouldBeOfType<JsonObject>();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Subsolo.slnx não encontrado acima de {AppContext.BaseDirectory}.");
    }
}
