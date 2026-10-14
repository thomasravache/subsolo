namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-05 — um projeto de exemplo com desreferência possivelmente nula, compilado sob o
/// <c>Directory.Build.props</c> do repositório, falha com CS8602 tratado como erro.
/// </summary>
public sealed class NullableAnalyzerTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Arquivos de configuração da raiz que definem o ambiente de build do repositório.</summary>
    private static readonly string[] BuildConfigurationFiles =
        ["Directory.Packages.props", "global.json", ".editorconfig"];

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task BuildFailsWithCS8602AsErrorUnderRepositoryBuildProps()
    {
        var ct = TestContext.Current.CancellationToken;
        var buildProps = RepositoryRoot.Combine("Directory.Build.props");
        File.Exists(buildProps).ShouldBeTrue("Directory.Build.props ausente na raiz do repositório");

        using var temp = new TempDirectory("subsolo-nullable-");
        File.Copy(buildProps, temp.Combine("Directory.Build.props"));
        foreach (var name in BuildConfigurationFiles)
        {
            var source = RepositoryRoot.Combine(name);
            if (File.Exists(source))
            {
                File.Copy(source, temp.Combine(name));
            }
        }

        var fixtureCopy = temp.Combine("NullableFixture");
        TempDirectory.CopyDirectory(
            RepositoryRoot.Combine("tests", "Subsolo.Tooling.Tests", "Fixtures", "NullableFixture"),
            fixtureCopy);

        var result = await ProcessRunner.RunAsync(
            "dotnet",
            ["build", "NullableFixture.csproj", "-nologo", "-p:RestoreLockedMode=false"],
            fixtureCopy,
            BuildTimeout,
            new Dictionary<string, string?> { ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1", ["DOTNET_NOLOGO"] = "1" },
            ct);

        result.ExitCode.ShouldNotBe(0, $"a build deveria falhar pela desreferência possivelmente nula:{Environment.NewLine}{result}");
        result.Output.ShouldContain("error CS8602", Case.Sensitive, $"CS8602 deveria ser erro, não aviso:{Environment.NewLine}{result}");
    }
}
