using System.Diagnostics;
using System.Reflection;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 IT-04 — com o Docker indisponível (<c>DOCKER_HOST</c> apontando para um socket inexistente), a
/// inicialização do <see cref="PostgresFixture"/> falha logo, com a mensagem "Docker não encontrado".
/// Roda num processo separado (Subsolo.Examples.DockerProbe): o Testcontainers resolve o endpoint do Docker uma
/// vez por processo, então mudar a variável aqui afetaria os outros testes.
/// </summary>
public sealed class DockerUnavailableTests
{
    private static readonly TimeSpan FailFastLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task FixtureFailsFastWithClearMessageWhenDockerIsUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingSocket = Path.Combine(Path.GetTempPath(), $"subsolo-no-docker-{Guid.NewGuid():N}.sock");
        var probeProject = RepositoryRoot.Combine("tests", "Examples", "Subsolo.Examples.DockerProbe");
        var configuration = typeof(DockerUnavailableTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

        var stopwatch = Stopwatch.StartNew();
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            ["run", "--no-build", "--configuration", configuration, "--project", probeProject],
            RepositoryRoot.Path,
            ProbeTimeout,
            new Dictionary<string, string?>
            {
                ["DOCKER_HOST"] = $"unix://{missingSocket}",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
            },
            ct);
        stopwatch.Stop();

        result.ExitCode.ShouldNotBe(0, $"o fixture não deveria subir sem Docker:{Environment.NewLine}{result}");
        result.Output.ShouldContain(
            "Docker não encontrado",
            Case.Sensitive,
            $"a falha deveria trazer a mensagem do contrato:{Environment.NewLine}{result}");
        stopwatch.Elapsed.ShouldBeLessThan(FailFastLimit, $"a falha deveria ser imediata:{Environment.NewLine}{result}");
    }
}
