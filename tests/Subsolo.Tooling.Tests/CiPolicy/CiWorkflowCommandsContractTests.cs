namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0006 CT-01 — os jobs do <c>ci.yml</c> chamam exatamente os comandos do sdd-config publicados pela
/// SPEC-0005@4 (<c>test</c>, <c>arch_test</c>, <c>coverage</c> no job <c>test</c>; <c>test_integration</c> no
/// <c>integration</c>; <c>test_e2e</c> no <c>e2e</c>) e os scripts da SPEC-0004 (<c>build</c>, <c>lint</c>).
/// Nenhuma lógica de build existe só no YAML: o comando aparece literalmente num step <c>run:</c> do job.
/// </summary>
public sealed class CiWorkflowCommandsContractTests
{
    [Theory]
    [Trait("Spec", "SPEC-0006:CT-01")]
    [InlineData("test", "test")]
    [InlineData("test", "arch_test")]
    [InlineData("test", "coverage")]
    [InlineData("integration", "test_integration")]
    [InlineData("e2e", "test_e2e")]
    public void JobRunsTheSddConfigCommand(string jobId, string sddConfigKey)
    {
        var command = CiWorkflowDocument.ReadSddConfigValue(sddConfigKey);
        command.ShouldNotBeNullOrWhiteSpace($"comando {sddConfigKey} vazio no sdd-config (aguarda SPEC-0005)");

        AssertJobRuns(jobId, command!, $"sdd-config.{sddConfigKey}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0006:CT-01")]
    [InlineData("build", "./tools/build.sh")]
    [InlineData("lint", "./tools/lint.sh")]
    public void JobRunsTheRepositoryScript(string jobId, string script) =>
        AssertJobRuns(jobId, script, script);

    private static void AssertJobRuns(string jobId, string command, string source)
    {
        var workflow = CiWorkflowDocument.Load();
        var expected = CiWorkflowDocument.NormalizeWhitespace(command);
        var scripts = workflow.NormalizedRunScripts(jobId);

        scripts.Any(script => script.Contains(expected, StringComparison.Ordinal)).ShouldBeTrue(
            $"job `{jobId}` deve chamar literalmente `{command}` ({source}) num step run:." +
            $"{Environment.NewLine}steps run: atuais:{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", scripts.DefaultIfEmpty("(nenhum)")));
    }
}
