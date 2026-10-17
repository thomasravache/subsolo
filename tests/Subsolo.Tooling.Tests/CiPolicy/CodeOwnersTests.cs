using System.Text;
using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0013 UT-03 (guarda) — os arquivos que controlam o processo e o gate do CI têm dono no CODEOWNERS: um PR que
/// altera o verificador vendorizado (<c>tools/sdd/**</c>, isento de escopo no sdd-config), o
/// <c>docs/specs/sdd-config.yml</c> ou os workflows exige a revisão do dono. Segue a semântica do GitHub: vale o
/// primeiro arquivo entre <c>.github/CODEOWNERS</c>, <c>CODEOWNERS</c> e <c>docs/CODEOWNERS</c>, e a última regra que
/// casa com o caminho (uma regra sem donos tira o dono do caminho).
/// </summary>
public sealed partial class CodeOwnersTests
{
    private static readonly string[] Locations = [".github/CODEOWNERS", "CODEOWNERS", "docs/CODEOWNERS"];

    [Theory]
    [Trait("Spec", "SPEC-0013:UT-03")]
    [InlineData("tools/sdd/spec_graph.py")]
    [InlineData("docs/specs/sdd-config.yml")]
    [InlineData(".github/workflows/ci.yml")]
    public void ProcessControlFileHasAnOwner(string path)
    {
        var location = Locations.FirstOrDefault(l => File.Exists(RepositoryRoot.Combine(l.Split('/'))));
        location.ShouldNotBeNull($"nenhum CODEOWNERS no repositório ({string.Join(", ", Locations)})");

        var rules = File.ReadAllLines(RepositoryRoot.Combine(location.Split('/')))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(parts => (Pattern: parts[0], Owners: parts.Skip(1).ToArray()))
            .ToList();

        var match = rules.LastOrDefault(rule => Matches(rule.Pattern, path));
        match.Pattern.ShouldNotBeNull($"nenhuma regra do {location} cobre {path}");
        match.Owners.ShouldNotBeEmpty(
            $"{path} ficou sem dono no {location}: a última regra que casa ({match.Pattern}) não tem donos");
    }

    /// <summary>Casamento no estilo do CODEOWNERS (subconjunto do .gitignore: /, *, **, diretório com / no fim).</summary>
    private static bool Matches(string pattern, string path)
    {
        var anchored = pattern.StartsWith('/') || pattern.TrimEnd('/').Contains('/', StringComparison.Ordinal);
        var body = pattern.TrimStart('/');
        var directory = body.EndsWith('/');
        body = body.TrimEnd('/');

        var regex = new StringBuilder(anchored ? "^" : "^(?:.*/)?");
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '*' && i + 1 < body.Length && body[i + 1] == '*')
            {
                regex.Append(".*");
                i++;
            }
            else if (body[i] == '*')
            {
                regex.Append("[^/]*");
            }
            else
            {
                regex.Append(Regex.Escape(body[i].ToString()));
            }
        }

        regex.Append(directory ? "/.*$" : "(?:/.*)?$");
        return Regex.IsMatch(path, regex.ToString(), RegexOptions.CultureInvariant);
    }
}
