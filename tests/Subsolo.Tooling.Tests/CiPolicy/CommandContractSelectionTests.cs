using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0013 UT-02 (guarda) — os testes <c>Kind=CommandContract</c> rodam os próprios comandos do sdd-config; se o
/// <c>test</c>, o <c>test_integration</c> ou o filtro padrão do <c>tools/coverage.sh</c> os selecionarem, a suíte chama a
/// si mesma (recursão). No CI cada comando já é um job, então nenhum job roda <c>test_commands_contract</c> nem um
/// <c>dotnet test</c> cujo filtro selecione <c>Kind=CommandContract</c>.
/// <para>
/// O filtro é avaliado contra um teste que só tem <c>Kind=CommandContract</c> (sintaxe <c>&amp;</c>, <c>|</c>,
/// <c>=</c>, <c>!=</c> do Microsoft.Testing.Platform, sem parênteses); <c>dotnet test</c> sem <c>--filter</c> seleciona tudo.
/// </para>
/// </summary>
public sealed partial class CommandContractSelectionTests
{
    private static readonly Dictionary<string, string> CommandContractTraits = new(StringComparer.Ordinal) { ["Kind"] = "CommandContract" };

    [Theory]
    [Trait("Spec", "SPEC-0013:UT-02")]
    [InlineData("test")]
    [InlineData("test_integration")]
    public void SddConfigSuiteCommandDoesNotSelectCommandContractTests(string key)
    {
        var command = CiWorkflowDocument.ReadSddConfigValue(key);
        command.ShouldNotBeNullOrWhiteSpace($"sdd-config.{key} vazio");

        var selecting = DotnetTestFiltersSelectingCommandContract(command!).ToList();
        selecting.ShouldBeEmpty(
            $"sdd-config.{key} não pode selecionar testes Kind=CommandContract (recursão: eles rodam os comandos do sdd-config); " +
            $"comando: {command}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:UT-02")]
    public void CoverageDefaultFilterDoesNotSelectCommandContractTests()
    {
        var script = File.ReadAllText(RepositoryRoot.Combine("tools", "coverage.sh"));
        var match = CoverageDefaultFilter().Match(script);
        match.Success.ShouldBeTrue("tools/coverage.sh deve declarar o filtro padrão como filter=\"${COVERAGE_FILTER:-<filtro>}\"");

        var filter = match.Groups["filter"].Value;
        Selects(filter, CommandContractTraits).ShouldBeFalse(
            $"o filtro padrão do tools/coverage.sh não pode selecionar Kind=CommandContract (recursão); filtro: {filter}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:UT-02")]
    public void NoCiJobRunsCommandContractTests()
    {
        var contractCommand = CiWorkflowDocument.ReadSddConfigValue("test_commands_contract");
        contractCommand.ShouldNotBeNullOrWhiteSpace("sdd-config.test_commands_contract vazio");
        var normalizedContract = CiWorkflowDocument.NormalizeWhitespace(contractCommand!);

        var workflow = CiWorkflowDocument.Load();
        var violations = new List<string>();
        foreach (var (job, _, _, run) in workflow.AllRunSteps())
        {
            var script = CiWorkflowDocument.NormalizeWhitespace(run);
            if (script.Contains(normalizedContract, StringComparison.Ordinal))
            {
                violations.Add($"job `{job}` roda sdd-config.test_commands_contract");
            }

            violations.AddRange(DotnetTestFiltersSelectingCommandContract(script).Select(f => $"job `{job}`: {f}"));
        }

        violations.ShouldBeEmpty("nenhum job do CI roda testes Kind=CommandContract (cada comando do sdd-config já é um job)");
    }

    /// <summary>Descrição de cada <c>dotnet test</c> do script cujo filtro seleciona <c>Kind=CommandContract</c>.</summary>
    private static IEnumerable<string> DotnetTestFiltersSelectingCommandContract(string script)
    {
        foreach (Match invocation in DotnetTest().Matches(script))
        {
            var arguments = invocation.Groups["args"].Value;
            var filter = FilterArgument().Match(arguments);
            if (!filter.Success)
            {
                if (TargetsOnlyProjectsWithoutCommandContractTests(arguments))
                {
                    continue;
                }

                yield return $"`dotnet test{arguments}` sem --filter (seleciona todos os testes, inclusive Kind=CommandContract)";
            }
            else if (Selects(filter.Groups["filter"].Value, CommandContractTraits))
            {
                yield return $"`--filter \"{filter.Groups["filter"].Value}\"` seleciona Kind=CommandContract";
            }
        }
    }

    /// <summary>
    /// <c>dotnet test</c> sem filtro é aceito só quando aponta um projeto de teste que não contém testes
    /// <c>Kind=CommandContract</c> (ex.: <c>dotnet test tests/Subsolo.ArchitectureTests</c>).
    /// </summary>
    private static bool TargetsOnlyProjectsWithoutCommandContractTests(string arguments)
    {
        var target = ProjectTarget().Match(arguments);
        if (!target.Success)
        {
            return false;
        }

        var projectDirectory = RepositoryRoot.Combine(target.Groups["path"].Value.TrimEnd('/').Split('/'));
        if (File.Exists(projectDirectory))
        {
            projectDirectory = Path.GetDirectoryName(projectDirectory)!;
        }

        return Directory.Exists(projectDirectory)
            && !Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Any(file => File.ReadAllText(file).Contains("\"Kind\", \"CommandContract\"", StringComparison.Ordinal));
    }

    /// <summary>Avalia um filtro de traits (OR de termos AND de condições Chave=Valor/Chave!=Valor) contra os traits de um teste.</summary>
    private static bool Selects(string filter, Dictionary<string, string> traits)
    {
        filter.IndexOfAny(['(', ')']).ShouldBe(-1, $"filtro com parênteses não suportado por esta guarda; atualize o teste: {filter}");

        return filter.Split('|').Any(term => term.Split('&').All(condition =>
        {
            var negated = condition.Contains("!=", StringComparison.Ordinal);
            var parts = condition.Split(negated ? "!=" : "=", 2);
            parts.Length.ShouldBe(2, $"condição de filtro inválida: '{condition}' em {filter}");
            var (name, value) = (parts[0].Trim(), parts[1].Trim());
            var matches = traits.TryGetValue(name, out var actual) && string.Equals(actual, value, StringComparison.Ordinal);
            return negated ? !matches : matches;
        }));
    }

    [GeneratedRegex(@"\bdotnet\s+test\b(?<args>(?:[^;&|)""]|""[^""]*"")*)")]
    private static partial Regex DotnetTest();

    [GeneratedRegex(@"(?:^|\s)(?:--project\s+)?(?<path>tests/[^\s""]+)")]
    private static partial Regex ProjectTarget();

    [GeneratedRegex(@"--filter(?:-query)?\s+""(?<filter>[^""]+)""")]
    private static partial Regex FilterArgument();

    [GeneratedRegex(@"filter=""\$\{COVERAGE_FILTER:-(?<filter>[^}]+)\}""")]
    private static partial Regex CoverageDefaultFilter();
}
