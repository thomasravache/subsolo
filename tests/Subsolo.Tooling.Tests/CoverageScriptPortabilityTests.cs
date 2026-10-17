using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0013 IT-02 e IT-03 — <c>tools/coverage.sh</c> falha rápido quando a base (<c>origin/main</c>) não existe, antes de
/// compilar e rodar a suíte, e é compatível com o bash 3.2 do macOS (sem <c>mapfile</c>/<c>readarray</c> nem outros
/// recursos do bash 4+, e sem expandir array possivelmente vazio sob <c>set -u</c>).
/// </summary>
public sealed partial class CoverageScriptPortabilityTests
{
    private const string BaseMissingMessage = "base origin/main não encontrada";
    private static readonly TimeSpan FailFastLimit = TimeSpan.FromSeconds(20);

    private static readonly (Regex Pattern, string Reason)[] Bash4OnlyFeatures =
    [
        (new Regex(@"\b(?:mapfile|readarray)\b", RegexOptions.CultureInvariant), "mapfile/readarray só existem no bash 4+"),
        (new Regex(@"\bdeclare\s+-[a-zA-Z]*A", RegexOptions.CultureInvariant), "array associativo (declare -A) é bash 4+"),
        (new Regex(@"\$\{[A-Za-z_][A-Za-z0-9_]*(?:\[[^]]*\])?(?:,,?|\^\^?)\}", RegexOptions.CultureInvariant), "${var,,}/${var^^} é bash 4+"),
        (new Regex(@"\|&|&>>", RegexOptions.CultureInvariant), "|& e &>> são bash 4+"),
        (new Regex(@"\bcoproc\b", RegexOptions.CultureInvariant), "coproc é bash 4+"),
        (new Regex(@"\bshopt\s+-s\s+(?:globstar|lastpipe)\b", RegexOptions.CultureInvariant), "globstar/lastpipe são bash 4+"),
    ];

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task MissingBaseFailsFastWithoutRunningTheSuite()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = RepositoryRoot.Combine("tools", "coverage.sh");
        File.Exists(source).ShouldBeTrue("tools/coverage.sh ausente");

        // Repositório mínimo, sem remoto: tools/coverage.sh e uma solution vazia (nada para compilar ou testar).
        using var temp = new TempDirectory("subsolo-coverage-base-");
        Directory.CreateDirectory(temp.Combine("tools"));
        File.Copy(source, temp.Combine("tools", "coverage.sh"));
        TempDirectory.CopyUnixMode(source, temp.Combine("tools", "coverage.sh"));
        await File.WriteAllTextAsync(temp.Combine("Subsolo.slnx"), "<Solution>\n</Solution>\n", ct);
        await GitAsync(temp.Path, ct, "init", "-q");
        await GitAsync(temp.Path, ct, "add", "-A");
        await GitAsync(temp.Path, ct, "-c", "user.name=coverage", "-c", "user.email=coverage@subsolo.test", "commit", "-q", "-m", "base");

        var stopwatch = Stopwatch.StartNew();
        var result = await ProcessRunner.RunAsync(
            "bash",
            ["./tools/coverage.sh"],
            temp.Path,
            TimeSpan.FromMinutes(5),
            new Dictionary<string, string?> { ["COVERAGE_BASE"] = null, ["COVERAGE_FILTER"] = null },
            ct);
        stopwatch.Stop();

        result.ExitCode.ShouldNotBe(0, $"coverage.sh sem a base deveria falhar:{Environment.NewLine}{result}");
        result.Output.ShouldContain(BaseMissingMessage, Case.Sensitive,
            $"coverage.sh deve dizer que a base não existe (mesma mensagem de hoje):{Environment.NewLine}{result}");
        result.Output.ShouldNotContain(">>> dotnet build", Case.Sensitive,
            $"coverage.sh deve conferir a base ANTES de compilar e rodar a suíte:{Environment.NewLine}{result}");
        stopwatch.Elapsed.ShouldBeLessThan(FailFastLimit, $"a falha pela base deve vir em poucos segundos:{Environment.NewLine}{result}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-03")]
    public void ScriptAvoidsBash4OnlyFeatures()
    {
        var lines = ShellLines(RepositoryRoot.Combine("tools", "coverage.sh"));

        var violations = new List<string>();
        foreach (var (number, line) in lines)
        {
            foreach (var (pattern, reason) in Bash4OnlyFeatures)
            {
                if (pattern.IsMatch(line))
                {
                    violations.Add($"linha {number}: {reason} — {line.Trim()}");
                }
            }
        }

        violations.ShouldBeEmpty("tools/coverage.sh deve rodar no bash 3.2 (macOS)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-03")]
    public void ArrayExpansionsAreSafeWhenEmptyUnderSetU()
    {
        var lines = ShellLines(RepositoryRoot.Combine("tools", "coverage.sh"));

        var unguarded = new List<string>();
        foreach (var (number, line) in lines)
        {
            foreach (Match expansion in ArrayExpansion().Matches(line))
            {
                var name = expansion.Groups["name"].Value;
                var guarded = $"${{{name}[@]+\"${{{name}[@]}}\"}}";
                var start = expansion.Index;
                var isGuarded = Enumerable.Range(0, line.Length)
                    .Where(i => line.AsSpan(i).StartsWith(guarded, StringComparison.Ordinal))
                    .Any(i => start >= i && start < i + guarded.Length);
                if (!isGuarded)
                {
                    unguarded.Add($"linha {number}: \"${{{name}[@]}}\" — {line.Trim()}");
                }
            }
        }

        unguarded.ShouldBeEmpty(
            "no bash < 4.4, expandir um array vazio sob `set -u` é erro (unbound variable): use " +
            "${nome[@]+\"${nome[@]}\"}");
    }

    /// <summary>Linhas do script fora do heredoc do Python e sem comentários de linha inteira.</summary>
    private static List<(int Number, string Line)> ShellLines(string path)
    {
        File.Exists(path).ShouldBeTrue($"{path} ausente");
        var result = new List<(int, string)>();
        string? heredocEnd = null;
        var number = 0;
        foreach (var line in File.ReadLines(path))
        {
            number++;
            if (heredocEnd is not null)
            {
                if (line.Trim() == heredocEnd)
                {
                    heredocEnd = null;
                }

                continue;
            }

            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            result.Add((number, line));
            var heredoc = Heredoc().Match(line);
            if (heredoc.Success)
            {
                heredocEnd = heredoc.Groups["end"].Value;
            }
        }

        return result;
    }

    private static async Task GitAsync(string directory, CancellationToken ct, params string[] arguments)
    {
        var result = await ProcessRunner.RunAsync("git", arguments, directory, TimeSpan.FromMinutes(1), cancellationToken: ct);
        result.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)} falhou:{Environment.NewLine}{result}");
    }

    [GeneratedRegex(@"""\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\[@\]\}""")]
    private static partial Regex ArrayExpansion();

    [GeneratedRegex(@"<<-?\s*'?""?(?<end>[A-Za-z_][A-Za-z0-9_]*)'?""?")]
    private static partial Regex Heredoc();
}
