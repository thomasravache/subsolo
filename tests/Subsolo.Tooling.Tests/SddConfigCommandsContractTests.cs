using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 CT-01 — os comandos <c>build</c> e <c>lint</c> gravados em <c>docs/specs/sdd-config.yml</c> executam com
/// exit 0 na raiz do repositório (contrato consumido pelas SPEC-0005, SPEC-0006 e SPEC-0007).
/// </summary>
/// <remarks>
/// O CT-01 da SPEC-0005 roda a suíte inteira aninhada com <see cref="Spec0005NestedRunVariable"/> = 1; nessa execução
/// este teste é pulado, porque a execução externa já o roda e dois <c>next build</c> simultâneos falham.
/// </remarks>
public sealed class SddConfigCommandsContractTests
{
    private const string Spec0005NestedRunVariable = "SUBSOLO_SPEC0005_CT01_NESTED";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(20);

    [Theory]
    [InlineData("build")]
    [InlineData("lint")]
    [Trait("Spec", "SPEC-0004:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task SddConfigCommandRunsSuccessfullyAtRepositoryRoot(string key)
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable(Spec0005NestedRunVariable) == "1",
            "execução aninhada do CT-01 da SPEC-0005: o CT-01 da SPEC-0004 já roda na execução externa");

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
