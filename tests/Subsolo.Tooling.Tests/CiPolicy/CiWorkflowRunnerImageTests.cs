using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0035 — o <c>ci.yml</c> roda numa imagem de runner fixa (<c>ubuntu-24.04</c>, nunca <c>ubuntu-latest</c>), o
/// cache dos navegadores do Playwright tem na chave a imagem do runner (<c>ImageOS</c> e <c>ImageVersion</c>, lidas num
/// passo anterior) além do hash do <c>e2e/pnpm-lock.yaml</c>, e o salvamento das imagens Docker dos Testcontainers é
/// tolerante: filtra a lista com <c>docker image inspect</c> antes do <c>docker image save</c> e não reprova o job.
/// </summary>
public sealed partial class CiWorkflowRunnerImageTests
{
    private const string PinnedRunner = "ubuntu-24.04";
    private const string E2eLockfile = "e2e/pnpm-lock.yaml";
    private const string SaveImagesStepName = "Salva imagens Docker usadas pelos testes";

    [Fact]
    [Trait("Spec", "SPEC-0035:UT-01")]
    public void EveryJobRunsOnThePinnedUbuntu2404Image()
    {
        var workflow = CiWorkflowDocument.Load();

        var runners = new List<(string Job, string RunsOn)>();
        foreach (var jobId in workflow.JobIds)
        {
            var runsOn = CiWorkflowDocument.Child(workflow.Job(jobId), "runs-on");
            if (runsOn is null)
            {
                // Job que chama workflow reutilizável (`uses:`) não declara runner.
                continue;
            }

            runners.Add((jobId, runsOn is YamlScalarNode scalar ? scalar.Value ?? string.Empty : runsOn.ToString()));
        }

        runners.ShouldNotBeEmpty("ci.yml deve ter jobs com `runs-on`");
        var violations = runners
            .Where(r => !string.Equals(r.RunsOn, PinnedRunner, StringComparison.Ordinal))
            .Select(r => $"job `{r.Job}`: runs-on `{r.RunsOn}`")
            .ToList();

        violations.ShouldBeEmpty(
            $"todo job deve usar `runs-on: {PinnedRunner}` (imagem fixa, ADR-0009); `ubuntu-latest` muda de versão sem aviso");
        runners.ShouldNotContain(r => r.RunsOn.Contains("latest", StringComparison.Ordinal),
            "nenhum job pode usar `ubuntu-latest`");
    }

    [Fact]
    [Trait("Spec", "SPEC-0035:UT-02")]
    public void PlaywrightBrowserCacheKeyIncludesRunnerImageAndE2eLockfileHash()
    {
        var workflow = CiWorkflowDocument.Load();

        var caches = new List<(string Job, int Index, string Key, IReadOnlyList<YamlMappingNode> Steps)>();
        foreach (var jobId in workflow.JobIds)
        {
            var steps = CiWorkflowDocument.Steps(workflow.Job(jobId));
            for (var i = 0; i < steps.Count; i++)
            {
                if (CiWorkflowDocument.Child(steps[i], "uses") is not YamlScalarNode uses
                    || !(uses.Value ?? string.Empty).StartsWith("actions/cache", StringComparison.Ordinal)
                    || CiWorkflowDocument.Child(steps[i], "with") is not YamlMappingNode with
                    || CiWorkflowDocument.Child(with, "path") is not YamlScalarNode path
                    || !(path.Value ?? string.Empty).Contains("ms-playwright", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = (CiWorkflowDocument.Child(with, "key") as YamlScalarNode)?.Value ?? string.Empty;
                caches.Add((jobId, i, key, steps));
            }
        }

        caches.ShouldNotBeEmpty("ci.yml deve ter um step actions/cache com path ~/.cache/ms-playwright");

        foreach (var (job, index, key, steps) in caches)
        {
            key.ShouldContain($"hashFiles('{E2eLockfile}')", Case.Sensitive,
                $"job `{job}`: a chave do cache do Playwright deve ter o hash de {E2eLockfile}; valor atual: '{key}'");

            var referencedIds = StepOutputReference().Matches(key).Select(m => m.Groups["id"].Value).ToHashSet(StringComparer.Ordinal);
            var imageSteps = steps
                .Take(index)
                .Where(step => CiWorkflowDocument.Child(step, "id") is YamlScalarNode id
                    && referencedIds.Contains(id.Value ?? string.Empty)
                    && CiWorkflowDocument.Child(step, "run") is YamlScalarNode run
                    && (run.Value ?? string.Empty).Contains("ImageOS", StringComparison.Ordinal)
                    && (run.Value ?? string.Empty).Contains("ImageVersion", StringComparison.Ordinal))
                .ToList();

            imageSteps.ShouldNotBeEmpty(
                $"job `{job}`: a chave do cache do Playwright deve usar `steps.<id>.outputs.*` de um passo anterior do mesmo job "
                + $"que lê ImageOS e ImageVersion (imagem do runner); valor atual: '{key}'");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0035:UT-03")]
    public void DockerImageSaveFiltersMissingImagesAndDoesNotFailTheJob()
    {
        var workflow = CiWorkflowDocument.Load();

        var matches = workflow.JobIds
            .SelectMany(jobId => CiWorkflowDocument.Steps(workflow.Job(jobId)).Select(step => (Job: jobId, Step: step)))
            .Where(s => CiWorkflowDocument.Child(s.Step, "name") is YamlScalarNode name
                && string.Equals(name.Value, SaveImagesStepName, StringComparison.Ordinal))
            .ToList();

        matches.ShouldNotBeEmpty($"ci.yml deve ter o passo \"{SaveImagesStepName}\"");

        foreach (var (job, step) in matches)
        {
            var continueOnError = (CiWorkflowDocument.Child(step, "continue-on-error") as YamlScalarNode)?.Value;
            continueOnError.ShouldBe("true",
                $"job `{job}`: o passo \"{SaveImagesStepName}\" deve ter `continue-on-error: true` (o cache é só uma otimização)");

            var run = (CiWorkflowDocument.Child(step, "run") as YamlScalarNode)?.Value ?? string.Empty;
            var script = CiWorkflowDocument.NormalizeWhitespace(run);
            var inspect = script.IndexOf("docker image inspect", StringComparison.Ordinal);
            var save = script.IndexOf("docker image save", StringComparison.Ordinal);

            save.ShouldBeGreaterThanOrEqualTo(0, $"job `{job}`: o passo \"{SaveImagesStepName}\" deve rodar `docker image save`");
            inspect.ShouldBeGreaterThanOrEqualTo(0,
                $"job `{job}`: o passo \"{SaveImagesStepName}\" deve filtrar a lista com `docker image inspect` "
                + "(imagens do `docker image ls` podem não existir mais, ex.: testcontainers/ryuk)");
            inspect.ShouldBeLessThan(save,
                $"job `{job}`: `docker image inspect` deve vir antes do `docker image save`");
        }
    }

    [GeneratedRegex(@"steps\.(?<id>[A-Za-z0-9_-]+)\.outputs\.[A-Za-z0-9_-]+")]
    private static partial Regex StepOutputReference();
}
