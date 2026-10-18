using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0022 IT-09 (ADR-0004, ADR-0015) — no <c>docker-compose.yaml</c> gerado por <c>aspire publish</c> do
/// <c>Subsolo.AppHost</c> e no <c>infra/caddy/Caddyfile</c>:
/// <list type="bullet">
/// <item>a API e o Caddy montam o mesmo volume nomeado <c>media</c>, e a API recebe <c>Media__Root</c> (efetivo com o
/// <c>.env</c> gerado) apontando para dentro do volume;</item>
/// <item>o Caddy tem a rota <c>/media/*</c> com <c>file_server</c>, <c>Cache-Control "public, max-age=31536000,
/// immutable"</c> e <c>X-Content-Type-Options nosniff</c> (no bloco ou no site), e o arquivo que ele procura para
/// <c>/media/flyers/&lt;nome&gt;</c> é o mesmo que a API grava em <c>Media__Root/flyers/&lt;nome&gt;</c> (mesmo lugar no
/// volume, considerando o <c>root</c> e se a rota tira o prefixo — <c>handle_path</c> — ou não — <c>handle</c>).</item>
/// </list>
/// Usa os auxiliares do <see cref="ComposePublishTests"/> (mesma leitura do Compose e do <c>.env</c>). Exige a Aspire CLI no PATH.
/// </summary>
public sealed partial class MediaComposePublishTests
{
    private const string ImmutableCache = "public, max-age=31536000, immutable";

    private const string SampleFile = "flyers/0123abcd-thumb.webp";

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-09")]
    [Trait("Kind", "Integration")]
    public async Task PublishedComposeSharesTheMediaVolumeBetweenApiAndCaddyAndCaddyServesItImmutable()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = Path.Combine(Path.GetTempPath(), $"subsolo-spec0022-it09-{Guid.NewGuid():N}");
        try
        {
            await ComposePublishTests.PublishAsync(output, ct);

            var compose = await File.ReadAllTextAsync(Path.Combine(output, "docker-compose.yaml"), ct);
            var envPath = Path.Combine(output, ".env");
            var env = File.Exists(envPath)
                ? ComposePublishTests.ParseEnvFile(await File.ReadAllLinesAsync(envPath, ct))
                : [];
            var services = ComposePublishTests.Services(compose);
            var api = services.SingleOrDefault(service => service.Name == "api").ShouldNotBeNull("serviço api ausente no Compose");
            var mounts = Mounts(compose);
            var listing = string.Join("\n", mounts.Select(mount => $"{mount.Service}: {mount.Source} → {mount.Target} ({mount.Type})"));

            var apiMedia = MediaVolume(mounts, "api").ShouldNotBeNull($"a api não monta o volume media:\n{listing}");
            var caddyMedia = MediaVolume(mounts, "caddy").ShouldNotBeNull($"o caddy não monta o volume media:\n{listing}");
            caddyMedia.Source.ShouldBe(apiMedia.Source, $"api e caddy montam volumes diferentes:\n{listing}");

            api.Environment.TryGetValue("Media__Root", out var declaredRoot).ShouldBeTrue("a api não recebe Media__Root no Compose");
            var mediaRoot = ComposePublishTests.Effective(declaredRoot!, env);
            var apiInVolume = Within(apiMedia.Target, $"{mediaRoot.TrimEnd('/')}/{SampleFile}")
                .ShouldNotBeNull($"Media__Root = \"{mediaRoot}\" fica fora do volume media montado em {apiMedia.Target} na api");

            var caddyfile = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "infra", "caddy", "Caddyfile"), ct);
            var route = MediaRoute(caddyfile).ShouldNotBeNull("o Caddyfile não tem rota (handle ou handle_path) para /media/*");
            route.Body.ShouldContain("file_server", Case.Sensitive, $"a rota /media/* não usa file_server:\n{route.Body}");
            HasHeader(route.Body, "Cache-Control", ImmutableCache)
                .ShouldBeTrue($"a rota /media/* não define Cache-Control \"{ImmutableCache}\":\n{route.Body}");
            (HasHeader(route.Body, "X-Content-Type-Options", "nosniff") || HasHeader(SiteLevel(caddyfile), "X-Content-Type-Options", "nosniff"))
                .ShouldBeTrue("o Caddy não manda X-Content-Type-Options nosniff em /media/*");

            var root = RootDirective().Match(route.Body);
            root.Success.ShouldBeTrue($"a rota /media/* não define root:\n{route.Body}");
            var requested = route.StripsPrefix ? $"/{SampleFile}" : $"/media/{SampleFile}";
            var caddyInVolume = Within(caddyMedia.Target, $"{root.Groups["path"].Value.TrimEnd('/')}{requested}")
                .ShouldNotBeNull($"o root {root.Groups["path"].Value} da rota /media/* fica fora do volume media montado em {caddyMedia.Target} no caddy");
            caddyInVolume.ShouldBe(
                apiInVolume,
                $"para /media/{SampleFile} o Caddy lê \"{caddyInVolume}\" do volume, mas a API grava em \"{apiInVolume}\"");
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    /// <summary>O volume nomeado <c>media</c> (ou terminado em <c>-media</c>/<c>_media</c>) montado em <paramref name="service"/>.</summary>
    private static Mount? MediaVolume(IEnumerable<Mount> mounts, string service) =>
        mounts.FirstOrDefault(mount => mount.Service == service
            && mount.Type == "volume"
            && (mount.Source == "media" || mount.Source.EndsWith("-media", StringComparison.Ordinal) || mount.Source.EndsWith("_media", StringComparison.Ordinal)));

    /// <summary>O caminho de <paramref name="path"/> relativo a <paramref name="mountTarget"/>, ou nulo se estiver fora dele.</summary>
    private static string? Within(string mountTarget, string path)
    {
        var target = mountTarget.TrimEnd('/') + "/";
        var normalized = Path.GetFullPath(path, "/");
        return normalized.StartsWith(target, StringComparison.Ordinal) ? normalized[target.Length..] : null;
    }

    /// <summary>Montagens de cada serviço do Compose (sintaxe curta <c>origem:destino[:ro]</c> ou longa).</summary>
    private static List<Mount> Mounts(string compose)
    {
        var yaml = new YamlStream();
        using (var reader = new StringReader(compose))
        {
            yaml.Load(reader);
        }

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var mounts = new List<Mount>();
        if (!root.Children.TryGetValue(new YamlScalarNode("services"), out var servicesNode) || servicesNode is not YamlMappingNode services)
        {
            return mounts;
        }

        foreach (var (key, value) in services.Children)
        {
            var name = ((YamlScalarNode)key).Value!;
            if (value is not YamlMappingNode service
                || !service.Children.TryGetValue(new YamlScalarNode("volumes"), out var volumesNode)
                || volumesNode is not YamlSequenceNode volumes)
            {
                continue;
            }

            foreach (var item in volumes.Children)
            {
                if (item is YamlScalarNode shortSyntax)
                {
                    var parts = (shortSyntax.Value ?? string.Empty).Split(':');
                    if (parts.Length >= 2)
                    {
                        var source = parts[0];
                        var type = source.StartsWith('.') || source.StartsWith('/') ? "bind" : "volume";
                        mounts.Add(new Mount(name, type, source, parts[1]));
                    }
                }
                else if (item is YamlMappingNode longSyntax)
                {
                    string Field(string field) =>
                        longSyntax.Children.TryGetValue(new YamlScalarNode(field), out var node) && node is YamlScalarNode scalar
                            ? scalar.Value ?? string.Empty
                            : string.Empty;
                    mounts.Add(new Mount(name, Field("type"), Field("source"), Field("target")));
                }
            }
        }

        return mounts;
    }

    /// <summary>O bloco <c>handle</c>/<c>handle_path</c> de <c>/media/*</c> (direto ou por um matcher nomeado com <c>path /media/*</c>).</summary>
    private static (string Body, bool StripsPrefix)? MediaRoute(string caddyfile)
    {
        var matchers = NamedPathMatcher().Matches(caddyfile)
            .Where(match => match.Groups["paths"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("/media/*"))
            .Select(match => "@" + match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (Match handle in HandleDirective().Matches(caddyfile))
        {
            var target = handle.Groups["target"].Value;
            if (target != "/media/*" && !matchers.Contains(target))
            {
                continue;
            }

            var open = handle.Index + handle.Length - 1;
            return (Block(caddyfile, open), handle.Groups["directive"].Value == "handle_path");
        }

        return null;
    }

    /// <summary>O conteúdo entre a chave em <paramref name="open"/> e a chave que a fecha.</summary>
    private static string Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            depth += text[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return text[(open + 1)..i];
            }
        }

        return text[(open + 1)..];
    }

    /// <summary>As linhas do bloco do site fora de qualquer <c>handle</c> (diretivas que valem para todas as rotas).</summary>
    private static string SiteLevel(string caddyfile) =>
        string.Join("\n", caddyfile.Split('\n').Where(line => line.StartsWith('\t') && !line.StartsWith("\t\t", StringComparison.Ordinal)));

    private static bool HasHeader(string block, string name, string value) =>
        block.Split('\n').Select(line => line.Trim()).Any(line =>
            line.StartsWith("header", StringComparison.Ordinal)
            && line.Contains(name, StringComparison.OrdinalIgnoreCase)
            && line.Contains(value, StringComparison.Ordinal));

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

    [GeneratedRegex(@"^\s*@(?<name>[\w-]+)\s+path\s+(?<paths>[^\n{]+)$", RegexOptions.Multiline)]
    private static partial Regex NamedPathMatcher();

    [GeneratedRegex(@"^\s*(?<directive>handle_path|handle)\s+(?<target>\S+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex HandleDirective();

    [GeneratedRegex(@"^\s*root\s+(?:\*\s+)?(?<path>/\S*)", RegexOptions.Multiline)]
    private static partial Regex RootDirective();

    private sealed record Mount(string Service, string Type, string Source, string Target);
}
