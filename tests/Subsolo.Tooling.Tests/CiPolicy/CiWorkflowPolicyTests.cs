using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0006 UT-01 — política do <c>.github/workflows/ci.yml</c>: os 8 jobs do contrato, permissões mínimas do
/// <c>GITHUB_TOKEN</c> (<c>security-events: write</c> só no <c>codeql</c>), <c>timeout-minutes</c> em todo job,
/// actions de terceiros fixadas por SHA de 40 caracteres, gatilhos do contrato e <c>concurrency</c> que cancela
/// execuções antigas.
/// </summary>
public sealed partial class CiWorkflowPolicyTests
{
    private const string Ci = CiWorkflowDocument.RelativePath;
    private static readonly string[] ContractTriggers = ["pull_request", "push", "workflow_dispatch"];
    private static readonly string[] BranchFilteredTriggers = ["pull_request", "push"];
    private static readonly string[] MainOnly = ["main"];

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void DeclaresExactlyTheEightContractJobs()
    {
        var workflow = CiWorkflowDocument.Load();

        workflow.JobIds.OrderBy(id => id, StringComparer.Ordinal).ShouldBe(
            CiWorkflowDocument.ContractJobIds.OrderBy(id => id, StringComparer.Ordinal),
            $"{Ci} deve ter exatamente os jobs do contrato: {string.Join(", ", CiWorkflowDocument.ContractJobIds)}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void TopLevelPermissionsAreMinimal()
    {
        var workflow = CiWorkflowDocument.Load();

        var permissions = CiWorkflowDocument.Child(workflow.Root, "permissions") as YamlMappingNode;
        permissions.ShouldNotBeNull($"{Ci} deve declarar `permissions` no nível do workflow como mapeamento (nunca read-all/write-all)");
        PermissionMap(permissions).ShouldBe(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["contents"] = "read", ["pull-requests"] = "read" },
            ignoreOrder: true,
            $"`permissions` do workflow deve ser exatamente contents: read e pull-requests: read");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void SecurityEventsWriteIsGrantedOnlyToCodeqlJob()
    {
        var workflow = CiWorkflowDocument.Load();

        foreach (var jobId in workflow.JobIds)
        {
            var permissionsNode = CiWorkflowDocument.Child(workflow.Job(jobId), "permissions");
            if (permissionsNode is YamlScalarNode scalar)
            {
                throw new ShouldAssertException(
                    $"job `{jobId}` usa `permissions: {scalar.Value}`; declare as permissões como mapeamento explícito");
            }

            var permissions = permissionsNode is YamlMappingNode map ? PermissionMap(map) : [];
            if (jobId == "codeql")
            {
                permissions.GetValueOrDefault("security-events").ShouldBe(
                    "write", "job `codeql` deve declarar `security-events: write` para publicar os alertas");
                permissions.Where(p => p.Key != "security-events" && p.Value == "write").ShouldBeEmpty(
                    "job `codeql` só pode ter `write` em security-events");
            }
            else
            {
                permissions.Where(p => p.Value == "write").ShouldBeEmpty(
                    $"job `{jobId}` não pode conceder permissão de escrita (security-events: write só no codeql)");
            }
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void EveryJobHasTimeoutMinutes()
    {
        var workflow = CiWorkflowDocument.Load();

        foreach (var jobId in workflow.JobIds)
        {
            var timeout = CiWorkflowDocument.Child(workflow.Job(jobId), "timeout-minutes") as YamlScalarNode;
            timeout.ShouldNotBeNull($"job `{jobId}` sem `timeout-minutes`");
            int.TryParse(timeout.Value, out var minutes).ShouldBeTrue($"`timeout-minutes` do job `{jobId}` deve ser um inteiro");
            minutes.ShouldBeGreaterThan(0, $"`timeout-minutes` do job `{jobId}` deve ser positivo");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void ThirdPartyActionsArePinnedByFullCommitSha()
    {
        var workflow = CiWorkflowDocument.Load();

        var uses = workflow.AllUses().ToList();
        uses.ShouldNotBeEmpty($"{Ci} não usa nenhuma action — esperado ao menos checkout");
        var unpinned = uses
            .Where(u => !u.Uses.StartsWith("./", StringComparison.Ordinal) && !PinnedBySha().IsMatch(u.Uses))
            .Select(u => $"{u.Job}: {u.Uses}")
            .ToList();
        unpinned.ShouldBeEmpty("toda action de terceiros deve ser fixada por SHA de 40 caracteres (owner/repo@<sha>)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void TriggersArePullRequestAndPushOnMainAndManualDispatch()
    {
        var workflow = CiWorkflowDocument.Load();

        var on = CiWorkflowDocument.Child(workflow.Root, "on") as YamlMappingNode;
        on.ShouldNotBeNull($"{Ci} deve declarar `on` como mapeamento (pull_request, push, workflow_dispatch)");
        on.Children.Keys.Select(CiWorkflowDocument.Scalar).ShouldBe(
            ContractTriggers,
            ignoreOrder: true,
            "gatilhos do contrato: pull_request (main), push (main), workflow_dispatch — e nenhum outro");

        foreach (var trigger in BranchFilteredTriggers)
        {
            var config = CiWorkflowDocument.Child(on, trigger) as YamlMappingNode;
            config.ShouldNotBeNull($"`on.{trigger}` deve restringir `branches: [main]`");
            var branches = CiWorkflowDocument.Child(config, "branches") as YamlSequenceNode;
            branches.ShouldNotBeNull($"`on.{trigger}.branches` ausente");
            branches.Children.Select(CiWorkflowDocument.Scalar).ShouldBe(MainOnly, ignoreOrder: false, $"`on.{trigger}.branches` deve ser [main]");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void ConcurrencyCancelsSupersededRuns()
    {
        var workflow = CiWorkflowDocument.Load();

        var concurrency = CiWorkflowDocument.Child(workflow.Root, "concurrency") as YamlMappingNode;
        concurrency.ShouldNotBeNull($"{Ci} deve declarar `concurrency` (mapeamento com group e cancel-in-progress)");
        var group = CiWorkflowDocument.Child(concurrency, "group") as YamlScalarNode;
        group.ShouldNotBeNull("`concurrency.group` ausente");
        group.Value.ShouldNotBeNullOrWhiteSpace("`concurrency.group` vazio");
        group.Value.ShouldContain("${{", Case.Sensitive, "`concurrency.group` deve variar por PR/ref (expressão ${{ ... }})");

        var cancel = CiWorkflowDocument.Child(concurrency, "cancel-in-progress") as YamlScalarNode;
        cancel.ShouldNotBeNull("`concurrency.cancel-in-progress` ausente");
        var value = cancel.Value ?? string.Empty;
        (value == "true" || value.StartsWith("${{", StringComparison.Ordinal)).ShouldBeTrue(
            $"`concurrency.cancel-in-progress` deve ser true (ou expressão que o liga nos PRs); valor: '{value}'");
    }

    private static Dictionary<string, string> PermissionMap(YamlMappingNode permissions) =>
        permissions.Children.ToDictionary(
            p => CiWorkflowDocument.Scalar(p.Key),
            p => CiWorkflowDocument.Scalar(p.Value),
            StringComparer.Ordinal);

    [GeneratedRegex("^[^@\\s]+@[0-9a-f]{40}$")]
    private static partial Regex PinnedBySha();
}
