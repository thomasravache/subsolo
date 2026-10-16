using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 CT-01 — os comandos <c>build</c> e <c>lint</c> gravados em <c>docs/specs/sdd-config.yml</c> executam com
/// exit 0 na raiz do repositório (contrato consumido pelas SPEC-0005, SPEC-0006 e SPEC-0007).
/// </summary>
/// <remarks>
/// Marcado <c>Kind=CommandContract</c> (SPEC-0005, Emenda v6): roda só com o comando <c>test_commands_contract</c>,
/// fora do <c>test</c> e do <c>test_integration</c> (no CI cada comando já é um job).
/// </remarks>
public sealed class SddConfigCommandsContractTests
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(20);

    [Theory]
    [InlineData("build")]
    [InlineData("lint")]
    [Trait("Spec", "SPEC-0004:CT-01")]
    [Trait("Kind", "CommandContract")]
    public async Task SddConfigCommandRunsSuccessfullyAtRepositoryRoot(string key)
    {
        var command = ReadSddConfigCommand(key);
        command.ShouldNotBeNullOrWhiteSpace($"comando {key} vazio no sdd-config (docs/specs/sdd-config.yml)");

        var result = await ProcessRunner.BashAsync(
            command!,
            RepositoryRoot.Path,
            CommandTimeout,
            new Dictionary<string, string?> { ["CI"] = "true", ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1", ["DOTNET_NOLOGO"] = "1" },
            TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, $"`{command}` (chave {key} do sdd-config) falhou na raiz:{Environment.NewLine}{result}");
    }

    private static string? ReadSddConfigCommand(string key)
    {
        var configPath = RepositoryRoot.Combine("docs", "specs", "sdd-config.yml");
        File.Exists(configPath).ShouldBeTrue("docs/specs/sdd-config.yml ausente");

        using var reader = new StreamReader(configPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        return root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }
}
