using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0006 UT-02 (Emenda v4) — todo job do <c>ci.yml</c> que roda um comando do sdd-config dependente de Node/pnpm
/// prepara o ambiente antes: um step <c>actions/setup-node</c> e um step que habilita o pnpm via corepack
/// (<c>corepack enable</c>). Dependem de Node/pnpm os comandos que chamam <c>tools/*.sh</c> ou <c>pnpm</c> e os comandos
/// <c>test</c>/<c>test_integration</c>, que executam os testes de integração do <c>tools/lint.sh</c> (chamam pnpm), e o
/// <c>test_commands_contract</c>, que roda os próprios comandos do sdd-config (inclusive <c>lint</c> e <c>test_e2e</c>) —
/// SPEC-0013, item 11.
/// </summary>
public sealed class CiWorkflowToolchainTests
{
    /// <summary>Comandos que rodam o <c>lint.sh</c> ou o pnpm indiretamente (pelos testes) sem citar pnpm literalmente.</summary>
    private static readonly string[] CommandsRunningLintIntegrationTests = ["test", "test_integration", "test_commands_contract"];

    /// <summary>Chaves de comando do sdd-config que podem aparecer num job do CI.</summary>
    private static readonly string[] SddConfigCommandKeys =
    [
        "build", "test", "test_integration", "test_commands_contract", "test_e2e", "smoke_test", "lint", "coverage",
        "mutation", "arch_test", "security_scan",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-02")]
    public void JobsRunningNodeDependentSddConfigCommandsSetUpNodeAndPnpmFirst()
    {
        var commands = NodeDependentSddConfigCommands();
        commands.Keys.ShouldContain("test_integration", "sdd-config.test_integration deve existir e depender de Node/pnpm");

        var workflow = CiWorkflowDocument.Load();
        var violations = new List<string>();

        foreach (var jobId in workflow.JobIds)
        {
            var steps = CiWorkflowDocument.Steps(workflow.Job(jobId));
            for (var index = 0; index < steps.Count; index++)
            {
                if (CiWorkflowDocument.Child(steps[index], "run") is not YamlScalarNode run)
                {
                    continue;
                }

                var script = CiWorkflowDocument.NormalizeWhitespace(run.Value ?? string.Empty);
                foreach (var (key, command) in commands)
                {
                    if (!script.Contains(command, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var previous = steps.Take(index).ToList();
                    if (!previous.Any(IsSetupNode))
                    {
                        violations.Add($"job `{jobId}` roda sdd-config.{key} (`{command}`) sem um step actions/setup-node antes");
                    }

                    if (!previous.Any(EnablesPnpmViaCorepack))
                    {
                        violations.Add($"job `{jobId}` roda sdd-config.{key} (`{command}`) sem um step `corepack enable` (pnpm via corepack) antes");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "jobs que rodam comandos do sdd-config dependentes de Node/pnpm (tools/*.sh, pnpm, test, test_integration, " +
            "test_commands_contract) " +
            "devem preparar Node (actions/setup-node) e pnpm (corepack enable) antes do comando");
    }

    private static Dictionary<string, string> NodeDependentSddConfigCommands()
    {
        var commands = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in SddConfigCommandKeys)
        {
            var value = CiWorkflowDocument.ReadSddConfigValue(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var command = CiWorkflowDocument.NormalizeWhitespace(value);
            var callsToolsScript = command.Contains("tools/", StringComparison.Ordinal) && command.Contains(".sh", StringComparison.Ordinal);
            var callsPnpm = command.Contains("pnpm", StringComparison.Ordinal);
            if (callsToolsScript || callsPnpm || CommandsRunningLintIntegrationTests.Contains(key, StringComparer.Ordinal))
            {
                commands[key] = command;
            }
        }

        return commands;
    }

    private static bool IsSetupNode(YamlMappingNode step) =>
        CiWorkflowDocument.Child(step, "uses") is YamlScalarNode uses
        && (uses.Value ?? string.Empty).StartsWith("actions/setup-node@", StringComparison.Ordinal);

    private static bool EnablesPnpmViaCorepack(YamlMappingNode step) =>
        CiWorkflowDocument.Child(step, "run") is YamlScalarNode run
        && CiWorkflowDocument.NormalizeWhitespace(run.Value ?? string.Empty).Contains("corepack enable", StringComparison.Ordinal);
}
