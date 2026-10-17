using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0013 UT-04 (guarda) — os jobs <c>test</c> e <c>integration</c> rodam testes <c>Kind=Integration</c> que chamam a
/// Aspire CLI (<c>aspire publish</c> do AppHost, SPEC-0008); cada um instala <c>Aspire.Cli</c> na versão do Aspire do
/// AppHost (<c>Aspire.AppHost.Sdk/&lt;versão&gt;</c>) num step anterior ao comando de testes do sdd-config.
/// </summary>
public sealed partial class CiWorkflowAspireCliTests
{
    [Theory]
    [Trait("Spec", "SPEC-0013:UT-04")]
    [InlineData("test", "test")]
    [InlineData("integration", "test_integration")]
    public void JobInstallsThePinnedAspireCliBeforeTheTests(string jobId, string sddConfigKey)
    {
        var appHost = File.ReadAllText(RepositoryRoot.Combine("src", "Subsolo.AppHost", "Subsolo.AppHost.csproj"));
        var sdk = AppHostSdkVersion().Match(appHost);
        sdk.Success.ShouldBeTrue("src/Subsolo.AppHost/Subsolo.AppHost.csproj sem Sdk=\"Aspire.AppHost.Sdk/<versão>\"");
        var version = sdk.Groups["version"].Value;

        var command = CiWorkflowDocument.ReadSddConfigValue(sddConfigKey);
        command.ShouldNotBeNullOrWhiteSpace($"sdd-config.{sddConfigKey} vazio");
        var normalizedCommand = CiWorkflowDocument.NormalizeWhitespace(command!);

        var workflow = CiWorkflowDocument.Load();
        var runs = CiWorkflowDocument.Steps(workflow.Job(jobId))
            .Select(step => CiWorkflowDocument.Child(step, "run") is YamlScalarNode run
                ? CiWorkflowDocument.NormalizeWhitespace(run.Value ?? string.Empty)
                : string.Empty)
            .ToList();

        var testsAt = runs.FindIndex(run => run.Contains(normalizedCommand, StringComparison.Ordinal));
        testsAt.ShouldBeGreaterThanOrEqualTo(0, $"job `{jobId}` não roda sdd-config.{sddConfigKey}");

        var installAt = runs.FindIndex(run => AspireCliInstall().Match(run) is { Success: true } m && m.Groups["version"].Value == version);
        installAt.ShouldBeGreaterThanOrEqualTo(0,
            $"job `{jobId}` deve instalar a Aspire CLI fixada: `dotnet tool install --global Aspire.Cli --version {version}` " +
            "(a versão do Aspire.AppHost.Sdk)");
        installAt.ShouldBeLessThan(testsAt, $"job `{jobId}` deve instalar a Aspire CLI antes do passo de testes");
    }

    [GeneratedRegex(@"Sdk=""Aspire\.AppHost\.Sdk/(?<version>[^""]+)""")]
    private static partial Regex AppHostSdkVersion();

    [GeneratedRegex(@"dotnet tool (?:install|update) (?:--global|-g) Aspire\.Cli --version (?<version>\S+)")]
    private static partial Regex AspireCliInstall();
}
