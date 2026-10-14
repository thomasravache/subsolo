using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0006 UT-01 — política do <c>.github/workflows/ci.yml</c>: os 8 jobs do contrato, permissões mínimas do
/// <c>GITHUB_TOKEN</c> (<c>security-events: write</c> só no <c>codeql</c>), <c>timeout-minutes</c> em todo job,
/// actions de terceiros fixadas por SHA de 40 caracteres, gatilhos do contrato e <c>concurrency</c> que cancela
/// execuções antigas. Guardas de segurança: nenhum <c>${{ github.event… }}</c>/<c>${{ github.head_ref }}</c> dentro de
/// <c>run:</c> (o <c>sdd</c> recebe título/corpo do PR por <c>env</c>), a política de auditoria do <c>security</c>
/// (Emenda v3: npm bloqueante em produção High/Critical e em Critical, aviso para High só de desenvolvimento; NuGet com
/// <c>--no-restore</c>) e imagens Docker fixadas por digest <c>@sha256</c>.
/// </summary>
public sealed partial class CiWorkflowPolicyTests
{
    private const string Ci = CiWorkflowDocument.RelativePath;
    private static readonly string[] ContractTriggers = ["pull_request", "push", "workflow_dispatch"];
    private static readonly string[] BranchFilteredTriggers = ["pull_request", "push"];
    private static readonly string[] MainOnly = ["main"];

    private static readonly string[] BlockingNpmAudits =
    [
        "(cd web && pnpm audit --prod --audit-level=high)",
        "(cd web && pnpm audit --audit-level=critical)",
    ];

    /// <summary>Opções do <c>docker run</c> que consomem o argumento seguinte (as demais são flags booleanas).</summary>
    private static readonly HashSet<string> DockerOptionsWithValue = new(StringComparer.Ordinal)
    {
        "-e", "--env", "--env-file", "-v", "--volume", "--mount", "-w", "--workdir", "-u", "--user", "--name",
        "--entrypoint", "--network", "--net", "-p", "--publish", "--platform", "-l", "--label", "--cpus", "-m",
        "--memory", "--add-host", "--cap-add", "--cap-drop", "--security-opt", "--tmpfs", "--pull", "-h", "--hostname",
    };

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

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void RunScriptsNeverInterpolateUntrustedEventData()
    {
        var workflow = CiWorkflowDocument.Load();

        var injectable = workflow.AllRunSteps()
            .Where(s => UntrustedExpression().IsMatch(s.Run))
            .Select(s => $"{s.Job}: {StepName(s.Step)} → {UntrustedExpression().Match(s.Run).Value}")
            .ToList();
        injectable.ShouldBeEmpty(
            "nenhum `run:` pode interpolar ${{ github.event… }} ou ${{ github.head_ref }} (script injection): " +
            "passe o valor por `env:` e leia a variável no script");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void SddJobReceivesPullRequestTitleAndBodyThroughEnv()
    {
        var workflow = CiWorkflowDocument.Load();
        var sdd = workflow.Job("sdd");

        var prCheck = CiWorkflowDocument.Steps(sdd)
            .SingleOrDefault(step => CiWorkflowDocument.Child(step, "run") is YamlScalarNode run
                && (run.Value ?? string.Empty).Contains("pr-check", StringComparison.Ordinal));
        prCheck.ShouldNotBeNull("job `sdd` deve ter exatamente um step `run:` com o `spec_graph.py pr-check`");
        var env = CiWorkflowDocument.Child(prCheck, "env") as YamlMappingNode;
        env.ShouldNotBeNull("o step do pr-check deve receber PR_TITLE/PR_BODY por `env:`");
        foreach (var (name, expression) in new[] { ("PR_TITLE", "github.event.pull_request.title"), ("PR_BODY", "github.event.pull_request.body") })
        {
            var value = CiWorkflowDocument.Child(env, name) as YamlScalarNode;
            value.ShouldNotBeNull($"o step do pr-check deve declarar `env.{name}`");
            CiWorkflowDocument.NormalizeWhitespace(value.Value ?? string.Empty).ShouldBe(
                $"${{{{ {expression} }}}}", $"`env.{name}` do pr-check deve vir de {expression}");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void SecurityJobRunsTheContractAuditPolicy()
    {
        var workflow = CiWorkflowDocument.Load();
        var steps = CiWorkflowDocument.Steps(workflow.Job("security"))
            .Select(step => (Step: step, Run: CiWorkflowDocument.Child(step, "run") is YamlScalarNode run
                ? CiWorkflowDocument.NormalizeWhitespace(run.Value ?? string.Empty)
                : null))
            .Where(s => s.Run is not null)
            .Select(s => (s.Step, Run: s.Run!))
            .ToList();

        foreach (var command in BlockingNpmAudits)
        {
            var blocking = steps.Where(s => s.Run.Contains(command, StringComparison.Ordinal)).ToList();
            blocking.ShouldNotBeEmpty($"job `security` deve rodar `{command}` (SPEC-0006 v3, bloqueante)");
            blocking.ShouldAllBe(
                s => !ContinuesOnError(s.Step) && !s.Run.Contains("|| true", StringComparison.Ordinal),
                $"`{command}` é bloqueante: o step não pode usar continue-on-error nem `|| true`");
        }

        var devOnly = steps
            .Where(s => DevOnlyHighAudit().IsMatch(s.Run) && !BlockingNpmAudits.Any(c => s.Run.Contains(c, StringComparison.Ordinal)))
            .ToList();
        devOnly.ShouldNotBeEmpty(
            "job `security` deve ter o step de aviso `(cd web && pnpm audit --audit-level=high)` para High só de desenvolvimento");
        devOnly.ShouldAllBe(
            s => ContinuesOnError(s.Step) || s.Run.EndsWith("exit 0", StringComparison.Ordinal),
            "o step de High só de desenvolvimento é aviso: não pode falhar o job (termine com `exit 0` ou use continue-on-error)");

        var nuget = steps.Where(s => s.Run.Contains("dotnet list package --vulnerable", StringComparison.Ordinal)).ToList();
        nuget.ShouldNotBeEmpty("job `security` deve rodar `dotnet list package --vulnerable`");
        nuget.ShouldAllBe(
            s => s.Run.Contains("--include-transitive", StringComparison.Ordinal)
                && s.Run.Contains("--no-restore", StringComparison.Ordinal)
                && !ContinuesOnError(s.Step),
            "`dotnet list package --vulnerable` deve usar --include-transitive e --no-restore (sem restore implícito com " +
            "auditoria) e ser bloqueante");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void DockerImagesArePinnedByDigest()
    {
        var workflow = CiWorkflowDocument.Load();
        var images = new List<(string Where, string Image)>();

        foreach (var (job, jobNode, step, run) in workflow.AllRunSteps())
        {
            var script = run.Replace("\\\r\n", " ", StringComparison.Ordinal).Replace("\\\n", " ", StringComparison.Ordinal);
            foreach (Match match in DockerRun().Matches(script))
            {
                var image = DockerRunImage(match.Groups["args"].Value);
                image.ShouldNotBeNull($"{job}: {StepName(step)} — não foi possível achar a imagem do `docker run`: {match.Value}");
                images.Add(($"{job}: {StepName(step)}", ResolveVariable(workflow, jobNode, step, image)));
            }
        }

        foreach (var (job, uses) in workflow.AllUses().Where(u => u.Uses.StartsWith("docker://", StringComparison.Ordinal)))
        {
            images.Add(($"{job}: uses", uses["docker://".Length..]));
        }

        foreach (var jobId in workflow.JobIds)
        {
            var jobNode = workflow.Job(jobId);
            if (CiWorkflowDocument.Child(jobNode, "container") is { } container)
            {
                images.Add(($"{jobId}: container", ContainerImage(container)));
            }

            if (CiWorkflowDocument.Child(jobNode, "services") is YamlMappingNode services)
            {
                foreach (var (name, service) in services.Children)
                {
                    images.Add(($"{jobId}: services.{CiWorkflowDocument.Scalar(name)}", ContainerImage(service)));
                }
            }
        }

        images.Where(i => !PinnedByDigest().IsMatch(i.Image)).Select(i => $"{i.Where} → {i.Image}").ToList().ShouldBeEmpty(
            "toda imagem Docker do workflow (docker run, docker://, container, services) deve ser fixada por digest " +
            "(imagem[:tag]@sha256:<64 hex>)");
    }

    private static string StepName(YamlMappingNode step) =>
        CiWorkflowDocument.Child(step, "name") is YamlScalarNode name ? name.Value ?? "(sem nome)" : "(sem nome)";

    private static bool ContinuesOnError(YamlMappingNode step) =>
        CiWorkflowDocument.Child(step, "continue-on-error") is YamlScalarNode value
        && !string.Equals(value.Value, "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Primeiro argumento posicional do <c>docker run</c> (a imagem), pulando opções e seus valores.</summary>
    private static string? DockerRunImage(string arguments)
    {
        var tokens = ShellToken().Matches(arguments).Select(m => m.Value).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith('-'))
            {
                return token;
            }

            if (!token.Contains('=', StringComparison.Ordinal) && DockerOptionsWithValue.Contains(token))
            {
                i++;
            }
        }

        return null;
    }

    /// <summary>Resolve <c>"$NOME"</c>/<c>${NOME}</c> pelo <c>env</c> do step, do job ou do workflow.</summary>
    private static string ResolveVariable(CiWorkflowDocument workflow, YamlMappingNode job, YamlMappingNode step, string token)
    {
        var unquoted = token.Trim('"', '\'');
        var variable = ShellVariable().Match(unquoted);
        if (!variable.Success)
        {
            return unquoted;
        }

        var name = variable.Groups["name"].Value;
        return workflow.EnvValue(job, step, name) ?? $"${name} (variável sem valor no env do step/job/workflow)";
    }

    private static string ContainerImage(YamlNode node) => node switch
    {
        YamlScalarNode scalar => scalar.Value ?? string.Empty,
        YamlMappingNode map when CiWorkflowDocument.Child(map, "image") is YamlScalarNode image => image.Value ?? string.Empty,
        _ => node.ToString(),
    };

    private static Dictionary<string, string> PermissionMap(YamlMappingNode permissions) =>
        permissions.Children.ToDictionary(
            p => CiWorkflowDocument.Scalar(p.Key),
            p => CiWorkflowDocument.Scalar(p.Value),
            StringComparer.Ordinal);

    [GeneratedRegex("^[^@\\s]+@[0-9a-f]{40}$")]
    private static partial Regex PinnedBySha();

    [GeneratedRegex(@"\$\{\{\s*github\.(?:event\b|head_ref\b)[^}]*\}\}")]
    private static partial Regex UntrustedExpression();

    [GeneratedRegex(@"\(cd web && pnpm audit --audit-level=high\)")]
    private static partial Regex DevOnlyHighAudit();

    [GeneratedRegex(@"\bdocker\s+run\b(?<args>[^\n;|&]*)")]
    private static partial Regex DockerRun();

    [GeneratedRegex(@"(?:""(?:[^""\\]|\\.)*""|'[^']*'|[^\s""'])+")]
    private static partial Regex ShellToken();

    [GeneratedRegex(@"^\$(?:\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|(?<name>[A-Za-z_][A-Za-z0-9_]*))$")]
    private static partial Regex ShellVariable();

    [GeneratedRegex("^[^@\\s]+@sha256:[0-9a-f]{64}$")]
    private static partial Regex PinnedByDigest();
}
