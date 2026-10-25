using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-07 — o <c>Subsolo.AppHost</c>, iniciado pelo <see cref="DistributedApplicationTestingBuilder"/>,
/// sobe os recursos (PostgreSQL, Seq, Mailpit, api, web); <c>api</c> e <c>web</c> ficam saudáveis e
/// <c>GET /health</c> da API responde 200. Exige Docker, Node.js e pnpm.
/// </summary>
[Collection(nameof(AppHostRunTests))]
public sealed class AppHostTests
{
    /// <summary>Folga para baixar imagens e instalar as dependências do front na primeira execução.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task AppHostStartsApiAndWebHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.StartAsync(ct).WaitAsync(StartupTimeout, ct);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("web", ct).WaitAsync(StartupTimeout, ct);

        using var client = app.CreateHttpClient("api");
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
