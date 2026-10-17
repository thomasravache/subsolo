using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0013 UT-01 — no <c>ci.yml</c>, o grupo de concorrência é único por commit fora de PR (<c>github.sha</c>, não
/// <c>github.ref</c>: com o ref, o GitHub cancela o run da <c>main</c> que está esperando e o commit fica sem CI) e os
/// caches do store do pnpm de <c>web/</c> e de <c>e2e/</c> têm chaves distintas (<c>pnpm-store-web-…</c> e
/// <c>pnpm-store-e2e-…</c>), cada uma com o hash só do próprio lockfile.
/// </summary>
public sealed partial class CiWorkflowConcurrencyAndCacheTests
{
    private const string WebLockfile = "web/pnpm-lock.yaml";
    private const string E2eLockfile = "e2e/pnpm-lock.yaml";

    [Fact]
    [Trait("Spec", "SPEC-0013:UT-01")]
    public void ConcurrencyGroupIsUniquePerCommitOutsidePullRequests()
    {
        var workflow = CiWorkflowDocument.Load();

        var concurrency = CiWorkflowDocument.Child(workflow.Root, "concurrency") as YamlMappingNode;
        concurrency.ShouldNotBeNull("ci.yml deve declarar `concurrency`");
        var group = (CiWorkflowDocument.Child(concurrency, "group") as YamlScalarNode)?.Value ?? string.Empty;

        group.ShouldContain("github.event.pull_request.number", Case.Sensitive,
            "`concurrency.group` deve agrupar por número do PR (um push novo no PR cancela o anterior)");
        group.ShouldContain("github.sha", Case.Sensitive,
            $"fora de PR, `concurrency.group` deve usar github.sha (único por commit); valor atual: '{group}'");
        group.ShouldNotContain("github.ref", Case.Sensitive,
            $"`concurrency.group` não pode usar github.ref: no push na main o GitHub cancela o run pendente; valor atual: '{group}'");
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:UT-01")]
    public void PnpmStoreCachesOfWebAndE2eHaveDistinctKeysHashingTheirOwnLockfile()
    {
        var workflow = CiWorkflowDocument.Load();
        var caches = PnpmStoreCacheSteps(workflow).ToList();
        caches.ShouldNotBeEmpty("ci.yml deve ter steps actions/cache do store do pnpm (path: steps.pnpm.outputs.store)");

        var violations = new List<string>();
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (job, key, restoreKeys) in caches)
        {
            var match = PnpmStoreKey().Match(key);
            if (!match.Success)
            {
                violations.Add($"job `{job}`: chave `{key}` deve começar com pnpm-store-web- ou pnpm-store-e2e-");
                continue;
            }

            var project = match.Groups["project"].Value;
            prefixes.Add(project);
            var (own, other) = project == "web" ? (WebLockfile, E2eLockfile) : (E2eLockfile, WebLockfile);
            var hashed = HashFilesArguments(key);
            if (!hashed.Contains(own, StringComparer.Ordinal))
            {
                violations.Add($"job `{job}`: chave `{key}` deve ter o hash de {own}");
            }

            if (hashed.Contains(other, StringComparer.Ordinal))
            {
                violations.Add($"job `{job}`: chave `{key}` (pnpm-store-{project}-) não pode misturar o hash de {other}");
            }

            foreach (var restore in restoreKeys)
            {
                if (!restore.StartsWith($"pnpm-store-{project}-", StringComparison.Ordinal))
                {
                    violations.Add($"job `{job}`: restore-key `{restore}` deve ter o mesmo prefixo pnpm-store-{project}- da chave");
                }
            }
        }

        violations.ShouldBeEmpty("caches do pnpm: uma chave por lockfile (web/ e e2e/ guardam pacotes diferentes)");
        prefixes.ShouldBe(["e2e", "web"], ignoreOrder: true,
            "o ci.yml deve ter ao menos um cache pnpm-store-web- e um pnpm-store-e2e-");
    }

    private static IEnumerable<(string Job, string Key, IReadOnlyList<string> RestoreKeys)> PnpmStoreCacheSteps(CiWorkflowDocument workflow)
    {
        foreach (var jobId in workflow.JobIds)
        {
            foreach (var step in CiWorkflowDocument.Steps(workflow.Job(jobId)))
            {
                if (CiWorkflowDocument.Child(step, "uses") is not YamlScalarNode uses
                    || !(uses.Value ?? string.Empty).StartsWith("actions/cache", StringComparison.Ordinal)
                    || CiWorkflowDocument.Child(step, "with") is not YamlMappingNode with
                    || CiWorkflowDocument.Child(with, "path") is not YamlScalarNode path
                    || !(path.Value ?? string.Empty).Contains("steps.pnpm.outputs.store", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = (CiWorkflowDocument.Child(with, "key") as YamlScalarNode)?.Value ?? string.Empty;
                var restore = (CiWorkflowDocument.Child(with, "restore-keys") as YamlScalarNode)?.Value ?? string.Empty;
                yield return (jobId, key, [.. restore.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);
            }
        }
    }

    private static List<string> HashFilesArguments(string key) =>
        [.. HashFiles().Matches(key).SelectMany(m => QuotedArgument().Matches(m.Groups["args"].Value).Select(a => a.Groups["value"].Value))];

    [GeneratedRegex("^pnpm-store-(?<project>web|e2e)-")]
    private static partial Regex PnpmStoreKey();

    [GeneratedRegex(@"hashFiles\((?<args>[^)]*)\)")]
    private static partial Regex HashFiles();

    [GeneratedRegex("'(?<value>[^']*)'")]
    private static partial Regex QuotedArgument();
}
