using System.Text.RegularExpressions;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 IT-06 — num repositório com um commit que altera um arquivo coberto pela metade (das duas linhas
/// novas, os testes executam uma), <c>./tools/coverage.sh</c> gera o relatório com a porcentagem das linhas
/// alteradas daquele arquivo em relação a <c>origin/main</c>: 50%, e não a cobertura do arquivo inteiro (67%).
/// </summary>
/// <remarks>
/// O teste monta um repositório-sandbox mínimo (uma biblioteca e um projeto de testes xUnit v3, com as mesmas
/// regras de build e versões centrais da raiz) para não rodar a suíte inteira — e a si mesmo — de novo.
/// O relatório é procurado na saída do script e nos relatórios em texto gravados em <c>coverage/</c>.
/// </remarks>
public sealed partial class CoverageScriptTests
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CoverageTimeout = TimeSpan.FromMinutes(15);

    private const string ChangedFileName = "HalfCovered.cs";

    /// <summary>Arquivos de configuração da raiz que o sandbox herda, quando existem.</summary>
    private static readonly string[] SharedRootFiles =
    [
        "global.json",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        ".editorconfig",
        "NuGet.config",
        "nuget.config",
        Path.Combine(".config", "dotnet-tools.json"),
        "testconfig.json",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task CoverageReportShowsChangedLinesPercentageOfHalfCoveredFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = RepositoryRoot.Combine("tools", "coverage.sh");
        File.Exists(script).ShouldBeTrue("tools/coverage.sh ausente no repositório");

        using var sandbox = new TempDirectory("subsolo-coverage-");
        CopySharedConfiguration(sandbox);
        WriteBaseline(sandbox);

        await GitAsync(sandbox, ct, "init", "-q", "-b", "main");
        await GitAsync(sandbox, ct, "add", "-A");
        await GitAsync(sandbox, ct, "commit", "-q", "-m", "chore: baseline");
        await GitAsync(sandbox, ct, "update-ref", "refs/remotes/origin/main", "HEAD");
        await GitAsync(sandbox, ct, "switch", "-q", "-c", "feature/half-covered");

        WriteHalfCoveredChange(sandbox);
        await GitAsync(sandbox, ct, "add", "-A");
        await GitAsync(sandbox, ct, "commit", "-q", "-m", "feat: altera arquivo coberto pela metade");

        var result = await ProcessRunner.RunAsync(
            "bash",
            [Path.Combine("tools", "coverage.sh")],
            sandbox.Path,
            CoverageTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = null,
                ["RestoreLockedMode"] = "false",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
            },
            ct);

        var report = result.Output + Environment.NewLine + ReadTextReports(sandbox);
        var fileLines = report.Split('\n').Where(line => line.Contains(ChangedFileName, StringComparison.Ordinal)).ToList();
        fileLines.ShouldNotBeEmpty($"o relatório deveria citar {ChangedFileName}:{Environment.NewLine}{result}");
        fileLines.ShouldContain(
            line => FiftyPercent().IsMatch(line),
            $"o relatório deveria mostrar 50% das linhas alteradas de {ChangedFileName}:{Environment.NewLine}" +
            $"{string.Join(Environment.NewLine, fileLines)}{Environment.NewLine}{result}");
    }

    [GeneratedRegex(@"(?<![\d.,])50(?:[.,]0+)?\s?%")]
    private static partial Regex FiftyPercent();

    private static void CopySharedConfiguration(TempDirectory sandbox)
    {
        foreach (var relative in SharedRootFiles)
        {
            var source = RepositoryRoot.Combine(relative);
            if (File.Exists(source))
            {
                var target = sandbox.Combine(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
        }

        var tools = sandbox.Combine("tools");
        Directory.CreateDirectory(tools);
        var script = Path.Combine(tools, "coverage.sh");
        File.Copy(RepositoryRoot.Combine("tools", "coverage.sh"), script);
        TempDirectory.CopyUnixMode(RepositoryRoot.Combine("tools", "coverage.sh"), script);

        File.WriteAllText(sandbox.Combine(".gitignore"), "bin/\nobj/\nTestResults/\ncoverage/\n");
    }

    private static void WriteBaseline(TempDirectory sandbox)
    {
        File.WriteAllText(
            sandbox.Combine("Subsolo.slnx"),
            """
            <Solution>
              <Project Path="src/Subsolo.CoverageProbe/Subsolo.CoverageProbe.csproj" />
              <Project Path="tests/Subsolo.CoverageProbe.Tests/Subsolo.CoverageProbe.Tests.csproj" />
            </Solution>

            """);

        Write(
            sandbox,
            ["src", "Subsolo.CoverageProbe", "Subsolo.CoverageProbe.csproj"],
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Library</OutputType>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
            </Project>

            """);
        Write(sandbox, ["src", "Subsolo.CoverageProbe", ChangedFileName], HalfCoveredSource(withChange: false));

        Write(
            sandbox,
            ["tests", "Subsolo.CoverageProbe.Tests", "Subsolo.CoverageProbe.Tests.csproj"],
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" />
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="xunit.runner.visualstudio">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="../../src/Subsolo.CoverageProbe/Subsolo.CoverageProbe.csproj" />
              </ItemGroup>
            </Project>

            """);
        Write(sandbox, ["tests", "Subsolo.CoverageProbe.Tests", "HalfCoveredTests.cs"], HalfCoveredTestsSource(withChange: false));
    }

    private static void WriteHalfCoveredChange(TempDirectory sandbox)
    {
        Write(sandbox, ["src", "Subsolo.CoverageProbe", ChangedFileName], HalfCoveredSource(withChange: true));
        Write(sandbox, ["tests", "Subsolo.CoverageProbe.Tests", "HalfCoveredTests.cs"], HalfCoveredTestsSource(withChange: true));
    }

    /// <summary>
    /// Linha base coberta; a mudança acrescenta duas linhas executáveis (métodos de expressão), uma coberta pelos
    /// testes e outra não: 1/2 = 50% das linhas alteradas, 2/3 do arquivo.
    /// </summary>
    private static string HalfCoveredSource(bool withChange)
    {
        var change = withChange
            ? "\n    public static int Covered(int value) => value + 1;\n\n    public static int Uncovered(int value) => value - 1;\n"
            : string.Empty;
        return "namespace Subsolo.CoverageProbe;\n\npublic static class HalfCovered\n{\n" +
            "    public static int Baseline(int value) => value;\n" + change + "}\n";
    }

    private static string HalfCoveredTestsSource(bool withChange)
    {
        var change = withChange
            ? "\n    [Fact]\n    public void CoveredAddsOne() => Assert.Equal(2, HalfCovered.Covered(1));\n"
            : string.Empty;
        return "using Xunit;\n\nnamespace Subsolo.CoverageProbe.Tests;\n\npublic sealed class HalfCoveredTests\n{\n" +
            "    [Fact]\n    public void BaselineReturnsValue() => Assert.Equal(1, HalfCovered.Baseline(1));\n" + change + "}\n";
    }

    private static void Write(TempDirectory sandbox, string[] relative, string content)
    {
        var path = sandbox.Combine(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string ReadTextReports(TempDirectory sandbox)
    {
        var coverage = sandbox.Combine("coverage");
        if (!Directory.Exists(coverage))
        {
            return string.Empty;
        }

        var texts = Directory
            .EnumerateFiles(coverage, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText);
        return string.Join(Environment.NewLine, texts);
    }

    private static async Task GitAsync(TempDirectory sandbox, CancellationToken ct, params string[] arguments)
    {
        string[] identity =
        [
            "-c", "core.hooksPath=/dev/null",
            "-c", "commit.gpgsign=false",
            "-c", "user.name=Subsolo Tests",
            "-c", "user.email=tests@subsolo.test",
        ];
        var result = await ProcessRunner.RunAsync("git", [.. identity, .. arguments], sandbox.Path, GitTimeout, cancellationToken: ct);
        result.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)} falhou no sandbox:{Environment.NewLine}{result}");
    }
}
