using System.Net;
using System.Text.Json;
using Subsolo.Api.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0032 CT-01 (parte do OpenAPI) — o lembrete na véspera não tem entrada pública nova (§5): o OpenAPI gerado
/// (<c>GET /openapi/v1.json</c>) tem exatamente as operações (caminho e método) do snapshot versionado
/// <c>docs/api/openapi.v1.json</c>, e nenhum caminho de lembrete. A igualdade completa com o snapshot é o SPEC-0007:CT-01;
/// a forma do <c>IEventsCatalog</c> fica no <c>EventsCatalogRemindersContractTests</c> (Events.UnitTests).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ReminderOpenApiContractTests(PostgresFixture postgres)
{
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";

    [Fact]
    [Trait("Spec", "SPEC-0032:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheReminderAddsNoOperationToTheOpenApi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory("Development", postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var generated = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        using var snapshot = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), SnapshotRelativePath), ct));
        var operations = Operations(generated);
        operations.ShouldBe(Operations(snapshot), ignoreOrder: true, $"o OpenAPI ganhou ou perdeu operações em relação a {SnapshotRelativePath}");
        operations.Where(operation =>
                operation.Contains("reminder", StringComparison.OrdinalIgnoreCase)
                || operation.Contains("lembrete", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty("o lembrete na véspera não tem entrada pública");
    }

    private static List<string> Operations(JsonDocument document) =>
    [
        .. document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(method => $"{method.Name.ToUpperInvariant()} {path.Name}")),
    ];

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
