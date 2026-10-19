using System.Text.RegularExpressions;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0024 H23-04 (revisão da SPEC-0023) — no <c>infra/caddy/Caddyfile</c>, o matcher <c>@served</c> da rota
/// <c>/media/*</c> (o que recebe o <c>Cache-Control</c> imutável) tem <c>not path */</c>: o matcher <c>file</c> também
/// casa um diretório (caminho terminado em <c>/</c>), e um diretório nunca pode ganhar o cache de um ano. A resposta real
/// do Caddy é conferida no E2E <c>e2e/tests/shows/media.spec.ts</c>.
/// </summary>
public sealed partial class CaddyMediaCacheTests
{
    [Fact]
    [Trait("Spec", "SPEC-0024:H23-04")]
    public async Task TheServedMatcherOfTheImmutableCacheExcludesDirectories()
    {
        var caddyfile = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), "infra", "caddy", "Caddyfile"), TestContext.Current.CancellationToken);

        var served = ServedMatcher().Match(caddyfile);
        served.Success.ShouldBeTrue("o Caddyfile não tem o matcher @served { … } da rota /media/*");
        var lines = served.Groups["body"].Value.Split('\n').Select(line => line.Trim()).ToList();
        lines.ShouldContain("file", $"@served sem o matcher file:\n{served.Value}");
        lines.ShouldContain("not path */", $"@served sem \"not path */\" (um diretório ganharia o cache imutável):\n{served.Value}");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Subsolo.slnx não encontrado acima de {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex(@"@served\s*\{(?<body>[^}]*)\}")]
    private static partial Regex ServedMatcher();
}
