using System.Text.Json.Nodes;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0014 CT-01 — o snapshot <c>docs/api/openapi.v1.json</c> (igual ao documento gerado pela API, conferido pelo
/// CT-01 da SPEC-0007) publica <c>GET /api/v1/me</c> (200 <c>{id, email, displayName, roles}</c>, 401
/// <c>auth.required</c>, 429) e <c>POST /api/v1/auth/logout</c> (204, 400 <c>security.csrf_invalid</c>, 401
/// <c>auth.required</c>, 429), os dois com 503 <c>service.unavailable</c> (banco das sessões fora do ar; SPEC-0016 CT-03,
/// §7.6 d), e o cliente TypeScript <c>web/src/api/schema.d.ts</c> foi regenerado com eles (a
/// igualdade exata com o snapshot é o <c>pnpm api:check</c> do CI). O consumidor é a SPEC-0016.
/// </summary>
public sealed class UsersContractTests
{
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";
    private const string ClientRelativePath = "web/src/api/schema.d.ts";

    [Fact]
    [Trait("Spec", "SPEC-0014:CT-01")]
    [Trait("Spec", "SPEC-0016:CT-03")]
    public async Task MeIsPublishedWithTheContractSchemaAndErrors()
    {
        var operation = await OperationAsync("/api/v1/me", "get");

        var responses = operation["responses"].ShouldBeOfType<JsonObject>();
        responses.Select(response => response.Key).ShouldBe(["200", "401", "429", "503"], ignoreOrder: true);

        var schema = Resolve(await SnapshotAsync(), responses["200"]?["content"]?["application/json"]?["schema"])
            .ShouldBeOfType<JsonObject>("200 de /api/v1/me sem schema JSON");
        var properties = schema["properties"].ShouldBeOfType<JsonObject>();
        properties.Select(property => property.Key).ShouldBe(["id", "email", "displayName", "roles"], ignoreOrder: true);
        (properties["id"]?["format"]?.GetValue<string>()).ShouldBe("uuid");
        TypeOf(properties["email"]).ShouldContain("string");
        TypeOf(properties["displayName"]).ShouldContain("string");
        TypeOf(properties["displayName"]).ShouldContain("null", customMessage: "displayName é string|null");
        TypeOf(properties["roles"]).ShouldContain("array");
        schema["required"].ShouldBeOfType<JsonArray>().Select(name => name?.GetValue<string>())
            .ShouldBe(["id", "email", "displayName", "roles"], ignoreOrder: true);

        ShouldBeProblem(responses, "401", "auth.required");
        ShouldBeProblem(responses, "429", "security.rate_limited");
        ShouldBeProblem(responses, "503", "service.unavailable");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:CT-01")]
    [Trait("Spec", "SPEC-0016:CT-03")]
    public async Task LogoutIsPublishedWithTheContractErrors()
    {
        var operation = await OperationAsync("/api/v1/auth/logout", "post");

        var responses = operation["responses"].ShouldBeOfType<JsonObject>();
        responses.Select(response => response.Key).ShouldBe(["204", "400", "401", "429", "503"], ignoreOrder: true);
        (responses["204"]?["content"]).ShouldBeNull("204 não tem corpo");

        ShouldBeProblem(responses, "400", "security.csrf_invalid");
        ShouldBeProblem(responses, "401", "auth.required");
        ShouldBeProblem(responses, "429", "security.rate_limited");
        ShouldBeProblem(responses, "503", "service.unavailable");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:CT-01")]
    public async Task TypeScriptClientWasRegeneratedWithMeAndLogout()
    {
        var client = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), ClientRelativePath), TestContext.Current.CancellationToken);

        client.ShouldContain("\"/api/v1/me\"", Case.Sensitive, $"{ClientRelativePath} sem /api/v1/me: rode (cd web && pnpm api:generate)");
        client.ShouldContain("\"/api/v1/auth/logout\"", Case.Sensitive, $"{ClientRelativePath} sem /api/v1/auth/logout: rode (cd web && pnpm api:generate)");
    }

    private static void ShouldBeProblem(JsonObject responses, string status, string code)
    {
        var response = responses[status].ShouldNotBeNull($"resposta {status} ausente");
        (response["content"]?["application/problem+json"]?["schema"]).ShouldNotBeNull($"{status} sem ProblemDetails");
        response.ToJsonString().ShouldContain(code, Case.Sensitive, $"a resposta {status} não documenta o código {code}");
    }

    private static async Task<JsonObject> OperationAsync(string path, string method)
    {
        var snapshot = await SnapshotAsync();
        var paths = snapshot["paths"].ShouldBeOfType<JsonObject>();
        paths.ContainsKey(path).ShouldBeTrue($"{SnapshotRelativePath} sem {path}; caminhos: {string.Join(", ", paths.Select(p => p.Key))}");
        return (paths[path]?[method]).ShouldBeOfType<JsonObject>($"{SnapshotRelativePath} sem {method.ToUpperInvariant()} {path}");
    }

    /// <summary>Segue um <c>$ref</c> local (<c>#/components/schemas/X</c>).</summary>
    private static JsonNode? Resolve(JsonObject document, JsonNode? schema)
    {
        var reference = schema?["$ref"]?.GetValue<string>();
        if (reference is null)
        {
            return schema;
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
