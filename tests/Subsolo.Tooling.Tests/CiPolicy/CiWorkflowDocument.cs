using YamlDotNet.RepresentationModel;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// Leitura de <c>.github/workflows/ci.yml</c> e de <c>docs/specs/sdd-config.yml</c> para os testes de política da
/// SPEC-0006. Usa o modelo de representação do YamlDotNet (sem tipagem): <c>on</c> continua sendo a chave "on".
/// </summary>
internal sealed class CiWorkflowDocument
{
    public const string RelativePath = ".github/workflows/ci.yml";

    /// <summary>Jobs do contrato da SPEC-0006 (§6) — os nomes também são os checks obrigatórios da main.</summary>
    public static readonly string[] ContractJobIds = ["build", "lint", "test", "integration", "e2e", "security", "codeql", "sdd"];

    private CiWorkflowDocument(YamlMappingNode root)
    {
        Root = root;
    }

    public YamlMappingNode Root { get; }

    public static CiWorkflowDocument Load()
    {
        var path = RepositoryRoot.Combine(".github", "workflows", "ci.yml");
        File.Exists(path).ShouldBeTrue($"{RelativePath} ausente: o workflow de CI da SPEC-0006 ainda não existe");

        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        yaml.Documents.Count.ShouldBe(1, $"{RelativePath} deve ter exatamente um documento YAML");
        var root = yaml.Documents[0].RootNode as YamlMappingNode;
        root.ShouldNotBeNull($"{RelativePath} deve ser um mapeamento YAML na raiz");
        return new CiWorkflowDocument(root);
    }

    public YamlMappingNode Jobs
    {
        get
        {
            var jobs = Child(Root, "jobs") as YamlMappingNode;
            jobs.ShouldNotBeNull($"{RelativePath} sem o mapeamento `jobs`");
            return jobs;
        }
    }

    public IReadOnlyList<string> JobIds => [.. Jobs.Children.Keys.Select(Scalar)];

    public YamlMappingNode Job(string id)
    {
        var job = Child(Jobs, id) as YamlMappingNode;
        job.ShouldNotBeNull($"{RelativePath} sem o job `{id}`");
        return job;
    }

    /// <summary>Steps do job (lista vazia quando o job não tem <c>steps</c>, por exemplo um workflow reutilizável).</summary>
    public static IReadOnlyList<YamlMappingNode> Steps(YamlMappingNode job) =>
        Child(job, "steps") is YamlSequenceNode steps ? [.. steps.Children.OfType<YamlMappingNode>()] : [];

    /// <summary>Todos os valores <c>uses:</c> do workflow: dos steps e de jobs que chamam workflows reutilizáveis.</summary>
    public IEnumerable<(string Job, string Uses)> AllUses()
    {
        foreach (var (key, value) in Jobs.Children)
        {
            if (value is not YamlMappingNode job)
            {
                continue;
            }

            var jobId = Scalar(key);
            if (Child(job, "uses") is YamlScalarNode jobUses)
            {
                yield return (jobId, jobUses.Value ?? string.Empty);
            }

            foreach (var step in Steps(job))
            {
                if (Child(step, "uses") is YamlScalarNode uses)
                {
                    yield return (jobId, uses.Value ?? string.Empty);
                }
            }
        }
    }

    /// <summary>Scripts <c>run:</c> dos steps do job, com espaços em branco colapsados para comparação.</summary>
    public IReadOnlyList<string> NormalizedRunScripts(string jobId) =>
        [.. Steps(Job(jobId))
            .Select(step => Child(step, "run"))
            .OfType<YamlScalarNode>()
            .Select(run => NormalizeWhitespace(run.Value ?? string.Empty))];

    public static YamlNode? Child(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var child) ? child : null;

    public static string Scalar(YamlNode node) => node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : node.ToString();

    public static string NormalizeWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Valor de uma chave de nível raiz do <c>docs/specs/sdd-config.yml</c> (null quando a chave não existe).</summary>
    public static string? ReadSddConfigValue(string key)
    {
        var configPath = RepositoryRoot.Combine("docs", "specs", "sdd-config.yml");
        File.Exists(configPath).ShouldBeTrue("docs/specs/sdd-config.yml ausente");

        using var reader = new StreamReader(configPath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        return Child(root, key) is YamlScalarNode scalar ? scalar.Value : null;
    }
}
