using YamlDotNet.RepresentationModel;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 CT-01 — os comandos <c>test</c>, <c>test_integration</c>, <c>arch_test</c>, <c>test_e2e</c> e
/// <c>coverage</c> gravados em <c>docs/specs/sdd-config.yml</c> executam com exit 0 na raiz do repositório
/// (contrato consumido pela SPEC-0006 e pelas specs de módulo).
/// </summary>
/// <remarks>
/// Marcado <c>Kind=CommandContract</c> (Emenda v6): roda só com o comando <c>test_commands_contract</c>. Os comandos
/// <c>test</c>, <c>test_integration</c> e <c>coverage</c> não selecionam esse Kind, então não há execução aninhada.
/// </remarks>
public sealed class SddConfigTestCommandsContractTests
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(45);

    [Theory]
    [InlineData("test")]
    [InlineData("test_integration")]
    [InlineData("arch_test")]
    [InlineData("test_e2e")]
    [InlineData("coverage")]
    [Trait("Spec", "SPEC-0005:CT-01")]
    [Trait("Kind", "CommandContract")]
    public async Task SddConfigTestCommandRunsSuccessfullyAtRepositoryRoot(string key)
    {
        var command = ReadSddConfigCommand(key);
        command.ShouldNotBeNullOrWhiteSpace($"comando {key} vazio no sdd-config (docs/specs/sdd-config.yml)");

        var result = await ProcessRunner.BashAsync(
            command!,
            RepositoryRoot.Path,
            CommandTimeout,
            new Dictionary<string, string?>
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
            },
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
