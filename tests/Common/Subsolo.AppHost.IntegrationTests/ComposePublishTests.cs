using System.Diagnostics;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0008 IT-05 — <c>aspire publish</c> do <c>Subsolo.AppHost</c> num diretório temporário gera um
/// <c>docker-compose.yaml</c> em que, sem nada definido pelo operador (o <c>.env</c> gerado como está), a API roda
/// com <c>ASPNETCORE_ENVIRONMENT</c> efetivamente <c>Production</c> — valor literal, ou variável com padrão
/// <c>${X:-Production}</c>, ou variável preenchida no <c>.env</c>; valor vazio não serve — e nenhum serviço é o
/// dashboard do Aspire. Exige a Aspire CLI (<c>aspire</c>) no PATH.
/// </summary>
public sealed partial class ComposePublishTests
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(8);

    [Fact]
    [Trait("Spec", "SPEC-0008:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task PublishedComposeRunsApiInProductionByDefaultWithoutAspireDashboard()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = Path.Combine(Path.GetTempPath(), $"subsolo-it05-{Guid.NewGuid():N}");
        try
        {
            await PublishAsync(output, ct);

            var compose = await File.ReadAllTextAsync(Path.Combine(output, "docker-compose.yaml"), ct);
            var envPath = Path.Combine(output, ".env");
            var env = File.Exists(envPath) ? ParseEnvFile(await File.ReadAllLinesAsync(envPath, ct)) : [];
            var services = Services(compose);

            var problems = services
                .Where(service => service.Name.Contains("dashboard", StringComparison.OrdinalIgnoreCase)
                    || service.Image.Contains("aspire-dashboard", StringComparison.OrdinalIgnoreCase))
                .Select(service => $"dashboard do Aspire no Compose: {service.Name} ({service.Image})")
                .ToList();

            var api = services.SingleOrDefault(service => service.Name == "api");
            if (api is null)
            {
                problems.Add($"serviço api ausente; serviços: {string.Join(", ", services.Select(s => s.Name))}");
            }
            else if (!api.Environment.TryGetValue("ASPNETCORE_ENVIRONMENT", out var declared))
            {
                problems.Add("a API não declara ASPNETCORE_ENVIRONMENT no Compose");
            }
            else if (Effective(declared, env) != "Production")
            {
                problems.Add(
                    $"ASPNETCORE_ENVIRONMENT da API = \"{declared}\", efetivo \"{Effective(declared, env)}\" com o .env " +
                    $"gerado ({FormatEnv(env)}); esperado Production");
            }

            problems.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    /// <summary>
    /// Roda <c>aspire publish</c> em <paramref name="output"/>. <paramref name="configuration"/> são opções do AppHost
    /// passadas como variáveis de ambiente do processo (<c>Subsolo__AdminEmail</c> = <c>Subsolo:AdminEmail</c>).
    /// </summary>
    internal static async Task PublishAsync(
        string output, CancellationToken ct, IReadOnlyDictionary<string, string>? configuration = null)
    {
        var root = RepositoryRoot();
        var start = new ProcessStartInfo("aspire")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (name, value) in configuration ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        foreach (var argument in new[]
        {
            "publish", "--apphost", Path.Combine(root, "src", "Subsolo.AppHost", "Subsolo.AppHost.csproj"),
            "-o", output, "--non-interactive", "--nologo",
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("não foi possível iniciar a Aspire CLI (aspire)");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PublishTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        process.ExitCode.ShouldBe(0, $"aspire publish falhou:\n{await stdout}\n{await stderr}");
    }

    /// <summary>
    /// Valor efetivo de uma entrada do Compose com o <c>.env</c>: <c>${X}</c> e <c>${X-d}</c> (d só se X não existe)
    /// e <c>${X:-d}</c> (d se X não existe ou está vazio), como o Docker Compose interpola.
    /// </summary>
    internal static string Effective(string value, Dictionary<string, string> env) =>
        Interpolation().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            var hasValue = env.TryGetValue(name, out var current);
            if (!match.Groups["op"].Success)
            {
                return current ?? string.Empty;
            }

            var useDefault = match.Groups["op"].Value == ":-"
                ? string.IsNullOrEmpty(current)
                : !hasValue;
            return useDefault ? match.Groups["default"].Value : current!;
        });

    internal static Dictionary<string, string> ParseEnvFile(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#') && line.Contains('=', StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"', '\''), StringComparer.Ordinal);

    internal static string FormatEnv(Dictionary<string, string> env) =>
        string.Join(", ", env.Select(entry => $"{entry.Key}={entry.Value}"));

    internal static List<ComposeService> Services(string compose)
    {
        var yaml = new YamlStream();
        using (var reader = new StringReader(compose))
        {
            yaml.Load(reader);
        }

        var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
        if (!rootNode.Children.TryGetValue(new YamlScalarNode("services"), out var servicesNode)
            || servicesNode is not YamlMappingNode services)
        {
            return [];
        }

        return [.. services.Children.Select(entry =>
        {
            var service = (YamlMappingNode)entry.Value;
            var image = service.Children.TryGetValue(new YamlScalarNode("image"), out var imageNode)
                ? ((YamlScalarNode)imageNode).Value ?? string.Empty
                : string.Empty;
            var environment = new Dictionary<string, string>(StringComparer.Ordinal);
            if (service.Children.TryGetValue(new YamlScalarNode("environment"), out var envNode))
            {
                if (envNode is YamlMappingNode map)
                {
                    foreach (var variable in map.Children)
                    {
                        environment[((YamlScalarNode)variable.Key).Value!] = ((YamlScalarNode)variable.Value).Value ?? string.Empty;
                    }
                }
                else if (envNode is YamlSequenceNode list)
                {
                    foreach (var item in list.Children.OfType<YamlScalarNode>())
                    {
                        var parts = (item.Value ?? string.Empty).Split('=', 2);
                        environment[parts[0]] = parts.Length > 1 ? parts[1] : string.Empty;
                    }
                }
            }

            return new ComposeService(((YamlScalarNode)entry.Key).Value!, image, environment);
        })];
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

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:(?<op>:?-)(?<default>[^}]*))?\}")]
    private static partial Regex Interpolation();

    internal sealed record ComposeService(string Name, string Image, IReadOnlyDictionary<string, string> Environment);
}
