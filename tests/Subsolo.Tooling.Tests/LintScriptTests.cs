using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-03 — com um arquivo C# e um TS fora da formatação, <c>./tools/lint.sh</c> termina com exit ≠ 0
/// e lista os dois arquivos (roda todas as verificações, não para na primeira falha).
/// </summary>
public sealed partial class LintScriptTests
{
    private static readonly TimeSpan LintTimeout = TimeSpan.FromMinutes(20);

    private const string BadCSharpFileName = "LintProbeBadlyFormatted.cs";
    private const string BadTypeScriptFileName = "lint-probe-badly-formatted.ts";

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task LintFailsAndListsBadlyFormattedCSharpAndTypeScriptFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        File.Exists(RepositoryRoot.Combine("tools", "lint.sh")).ShouldBeTrue("tools/lint.sh ausente no repositório");
        var solution = RepositoryRoot.Combine("Subsolo.slnx");
        File.Exists(solution).ShouldBeTrue("Subsolo.slnx ausente na raiz do repositório");
        File.Exists(RepositoryRoot.Combine("web", "package.json")).ShouldBeTrue("web/package.json ausente (projeto Next.js não criado)");

        var projectDirectory = FirstSolutionProjectDirectory(solution);

        using var temp = new TempDirectory("subsolo-lint-");
        await RepositorySnapshot.CopyToAsync(temp.Path, ct);

        // C# compilável, porém fora do .editorconfig (whitespace/estilo), dentro de um projeto da solution.
        await File.WriteAllTextAsync(
            Path.Combine(temp.Path, projectDirectory, BadCSharpFileName),
            "namespace Subsolo.LintProbe{public static class LintProbeBadlyFormatted{public static int Value( ){return 1;}}}\n",
            ct);

        // TS válido, porém fora do Prettier.
        var webSource = Directory.Exists(Path.Combine(temp.Path, "web", "src"))
            ? Path.Combine(temp.Path, "web", "src")
            : Path.Combine(temp.Path, "web");
        await File.WriteAllTextAsync(
            Path.Combine(webSource, BadTypeScriptFileName),
            "export const   lintProbe = {a:1,b:  2}\n",
            ct);

        // Algumas ferramentas usam o índice do git (ex.: arquivos a verificar); a cópia vira um repositório próprio.
        await ProcessRunner.RunAsync("git", ["init", "-q"], temp.Path, TimeSpan.FromMinutes(1), cancellationToken: ct);
        await ProcessRunner.RunAsync("git", ["add", "-A"], temp.Path, TimeSpan.FromMinutes(1), cancellationToken: ct);

        var result = await ProcessRunner.RunAsync(
            "bash",
            ["./tools/lint.sh"],
            temp.Path,
            LintTimeout,
            new Dictionary<string, string?> { ["CI"] = "true" },
            ct);

        result.ExitCode.ShouldNotBe(0, $"lint deveria falhar com arquivos fora da formatação:{Environment.NewLine}{result}");
        result.Output.ShouldContain(BadCSharpFileName, Case.Sensitive, $"lint deve listar o arquivo C#:{Environment.NewLine}{result}");
        result.Output.ShouldContain(BadTypeScriptFileName, Case.Sensitive, $"lint deve listar o arquivo TS:{Environment.NewLine}{result}");
    }

    /// <summary>Diretório (relativo à raiz) do primeiro projeto listado no Subsolo.slnx.</summary>
    private static string FirstSolutionProjectDirectory(string solution)
    {
        var match = ProjectPathPattern().Match(File.ReadAllText(solution));
        match.Success.ShouldBeTrue("Subsolo.slnx não lista nenhum projeto (<Project Path=\"...csproj\" />)");
        var projectPath = match.Groups["path"].Value.Replace('\\', '/');
        return Path.GetDirectoryName(projectPath) ?? string.Empty;
    }

    [GeneratedRegex(@"<Project\s+Path=""(?<path>[^""]+\.csproj)""", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectPathPattern();
}
