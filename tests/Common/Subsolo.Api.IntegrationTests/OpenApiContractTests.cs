using System.Net;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Subsolo.Api.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0007 CT-01 — <c>GET /openapi/v1.json</c> devolve um documento OpenAPI 3.1 válido e igual ao snapshot
/// versionado em <c>docs/api/openapi.v1.json</c> (contrato consumido pelo cliente TypeScript). A comparação é
/// semântica (JSON) e ignora <c>servers</c>, que depende do endereço em que a API roda.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class OpenApiContractTests(PostgresFixture postgres)
{
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";

    [Fact]
    [Trait("Spec", "SPEC-0007:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task OpenApiDocumentIsValid31AndMatchesVersionedSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory("Development", postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(ct);

        var read = OpenApiDocument.Parse(json, "json");
        read.Diagnostic.ShouldNotBeNull();
        read.Diagnostic.Errors.ShouldBeEmpty(string.Join("; ", read.Diagnostic.Errors.Select(e => e.ToString())));
        read.Diagnostic.SpecificationVersion.ShouldBe(OpenApiSpecVersion.OpenApi3_1);
        read.Document.ShouldNotBeNull();

        var snapshotPath = Path.Combine(RepositoryRoot(), SnapshotRelativePath);
        File.Exists(snapshotPath).ShouldBeTrue(
            $"snapshot ausente: gere {SnapshotRelativePath} a partir de GET /openapi/v1.json e versione o arquivo");
        var snapshot = await File.ReadAllTextAsync(snapshotPath, ct);

        var actualNode = Normalize(json);
        var snapshotNode = Normalize(snapshot);
        JsonNode.DeepEquals(actualNode, snapshotNode).ShouldBeTrue(
            $"o OpenAPI gerado diverge do snapshot {SnapshotRelativePath}; atualize o snapshot se a mudança de contrato " +
            $"for intencional.\nGerado:\n{actualNode?.ToJsonString()}");
    }

    private static JsonNode? Normalize(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is JsonObject document)
        {
            document.Remove("servers");
        }

        return node;
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
