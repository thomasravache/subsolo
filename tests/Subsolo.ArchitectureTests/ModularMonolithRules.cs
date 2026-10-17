using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Xunit.Sdk;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Architecture = ArchUnitNET.Domain.Architecture;
using IType = ArchUnitNET.Domain.IType;

namespace Subsolo.ArchitectureTests;

/// <summary>
/// SPEC-0007 — regras do ADR-0001 aplicadas a todo módulo (<c>Subsolo.Modules.&lt;M&gt;.*</c>). As camadas são
/// reconhecidas pelos namespaces <c>Subsolo.Modules.&lt;M&gt;.{Domain,Application,Infrastructure,Presentation,
/// IntegrationEvents}</c>. Cada regra falha nomeando a regra (a constante) e os tipos da dependência proibida.
/// </summary>
public static partial class ModularMonolithRules
{
    /// <summary><c>&lt;M&gt;.Domain</c> não depende de Application, Infrastructure, Presentation, EF Core nem ASP.NET.</summary>
    public const string DomainIsolation =
        "ADR-0001: o Domain do módulo não depende de Application, Infrastructure, Presentation, EF Core nem ASP.NET";

    /// <summary><c>&lt;M&gt;.Application</c> não depende de Infrastructure nem de Presentation.</summary>
    public const string ApplicationIsolation =
        "ADR-0001: o Application do módulo não depende de Infrastructure nem de Presentation";

    /// <summary>Um módulo não referencia outro módulo, exceto o <c>&lt;Outro&gt;.IntegrationEvents</c>.</summary>
    public const string ModuleBoundaries =
        "ADR-0001: um módulo só referencia outro módulo pelo IntegrationEvents dele";

    /// <summary>
    /// <c>&lt;M&gt;.IntegrationEvents</c> não depende de nada do Subsolo além de Common.Application e dos tipos de
    /// resultado de Common.Domain (<c>Result</c>, <c>Result&lt;T&gt;</c>, <c>Error</c>, <c>ErrorType</c>; esclarecimento do
    /// ADR-0001 na SPEC-0018). Entidades e eventos de domínio continuam proibidos.
    /// </summary>
    public const string IntegrationEventsDependencies =
        "ADR-0001: o IntegrationEvents do módulo só depende de Common.Application e de Result, Result<T>, Error e ErrorType de Common.Domain";

    /// <summary>Não há ciclos de dependência entre módulos.</summary>
    public const string NoModuleCycles = "ADR-0001: não há ciclos de dependência entre módulos";

    /// <summary>
    /// SPEC-0018 — fora do módulo Notifications, nenhum tipo do Subsolo usa as bibliotecas de SMTP (MailKit, MimeKit).
    /// </summary>
    public const string SmtpOnlyInNotifications =
        "ADR-0014: só o módulo Notifications usa bibliotecas de SMTP (MailKit, MimeKit)";

    /// <summary>
    /// Os únicos tipos de Common.Domain permitidos no IntegrationEvents (nome completo, depois de <c>Subsolo</c>):
    /// <c>Result</c>, <c>Result&lt;T&gt;</c> (<c>Result`1</c>), <c>Error</c> e <c>ErrorType</c>.
    /// </summary>
    private const string AllowedResultTypes = @"\.Common\.Domain\.(?:Result|Result`1|Error|ErrorType)$";

    private const string Root = ArchitectureRules.ModulesRoot;
    private static readonly string RootPattern = Regex.Escape(Root);

    /// <summary>Pastas do repositório onde ficam os projetos de módulo.</summary>
    private static readonly string[] ProjectFolders = ["src", "tests"];

    /// <summary>Todos os assemblies <c>Subsolo.Modules.*</c> ao lado do assembly de teste.</summary>
    public static IReadOnlyList<Assembly> ModuleAssemblies() =>
        [.. Directory
            .EnumerateFiles(AppContext.BaseDirectory, $"{Root}.*.dll")
            .Order(StringComparer.Ordinal)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))];

    /// <summary>
    /// SPEC-0018 — os assemblies em que a regra <see cref="SmtpOnlyInNotifications"/> vale: todos os módulos
    /// (<see cref="ModuleAssemblies"/>) e os blocos <c>Subsolo.Common.*</c> ao lado do assembly de teste (sem o dublê de
    /// violações do ADR-0001).
    /// </summary>
    public static IReadOnlyList<Assembly> SmtpRuleAssemblies() =>
        [.. ModuleAssemblies(), .. Directory
            .EnumerateFiles(AppContext.BaseDirectory, "Subsolo.Common.*.dll")
            .Where(path => !Path.GetFileName(path).StartsWith("Subsolo.Common.ArchitectureViolations", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))];

    /// <summary>
    /// Nomes (sem extensão) de todos os projetos <c>Subsolo.Modules.*.csproj</c> em <c>src/</c> e <c>tests/</c> de
    /// <paramref name="repositoryRoot"/>.
    /// </summary>
    public static IReadOnlyList<string> ModuleProjects(string repositoryRoot) =>
        [.. ProjectFolders
            .Select(folder => Path.Combine(repositoryRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, $"{Root}.*.csproj", SearchOption.AllDirectories))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Falha nomeando cada projeto <c>Subsolo.Modules.*.csproj</c> do repositório (<see cref="ModuleProjects"/>) que
    /// não está entre os assemblies <paramref name="loaded"/> avaliados pelas regras (módulo esquecido no projeto de
    /// testes de arquitetura).
    /// </summary>
    public static void CheckEveryModuleIsLoaded(string repositoryRoot, IEnumerable<Assembly> loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var loadedNames = loaded.Select(assembly => assembly.GetName().Name).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var missing = ModuleProjects(repositoryRoot).Where(project => !loadedNames.Contains(project)).ToList();
        if (missing.Count > 0)
        {
            throw new XunitException(
                "ADR-0001: todo módulo é avaliado pelas regras de arquitetura; referencie no " +
                $"Subsolo.ArchitectureTests.csproj: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// Avalia a regra <paramref name="rule"/> (uma das constantes desta classe) sobre <paramref name="assemblies"/>
    /// e falha (exceção do xUnit/ArchUnitNET) com uma mensagem que contém o texto da regra (o <c>because</c> de cada
    /// regra) e os tipos ou o ciclo que a violam.
    /// </summary>
    public static void Check(string rule, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0)
        {
            throw new ArgumentException("Informe ao menos um assembly.", nameof(assemblies));
        }

        var architecture = new ArchLoader().LoadAssemblies(assemblies).Build();
        if (rule == NoModuleCycles)
        {
            CheckNoModuleCycles(architecture);
            return;
        }

        foreach (var archRule in Rules(rule, Modules(architecture)))
        {
            archRule.Check(architecture);
        }
    }

    private static IEnumerable<IArchRule> Rules(string rule, IReadOnlyList<string> modules) => rule switch
    {
        DomainIsolation =>
        [
            ForbidDependencies(
                Layer("[^.]+", "Domain"),
                $"^(?:{RootPattern}\\.[^.]+\\.(?:Application|Infrastructure|Presentation)|Microsoft\\.EntityFrameworkCore|Microsoft\\.AspNetCore)(?:\\..*)?$",
                rule),
        ],
        ApplicationIsolation =>
        [
            ForbidDependencies(
                Layer("[^.]+", "Application"),
                $"^{RootPattern}\\.[^.]+\\.(?:Infrastructure|Presentation)(?:\\..*)?$",
                rule),
        ],
        ModuleBoundaries => modules.Select(module => ForbidDependencies(
            $"^{RootPattern}\\.{Regex.Escape(module)}(?:\\..*)?$",
            $"^{RootPattern}\\.(?!{Regex.Escape(module)}(?:\\.|$))[^.]+(?:\\.(?!IntegrationEvents(?:\\.|$)).*)?$",
            rule)),
        IntegrationEventsDependencies => modules.Select(module => ForbidDependenciesByFullName(
            Layer(Regex.Escape(module), "IntegrationEvents"),
            $"^Subsolo(?!\\.Common\\.Application\\.)(?!\\.Modules\\.{Regex.Escape(module)}\\.IntegrationEvents\\.)(?!{AllowedResultTypes})(?:\\..*)?$",
            rule)),
        SmtpOnlyInNotifications =>
        [
            ForbidDependencies(
                "^Subsolo(?!\\.Modules\\.Notifications(?:\\.|$))(?:\\..*)?$",
                "^(?:MailKit|MimeKit)(?:\\..*)?$",
                rule),
        ],
        _ => throw new ArgumentException($"Regra desconhecida: \"{rule}\".", nameof(rule)),
    };

    /// <summary>
    /// Grafo de dependências entre módulos (um tipo do módulo A que usa um tipo do módulo B é a aresta A → B) e
    /// busca em profundidade por um ciclo; falha nomeando a regra e o ciclo (ex.: <c>CycleA → CycleB → CycleA</c>).
    /// </summary>
    private static void CheckNoModuleCycles(Architecture architecture)
    {
        var edges = architecture.Types
            .Select(type => (From: ModuleOf(type), Targets: type.Dependencies.Select(d => ModuleOf(d.Target))))
            .Where(source => source.From is not null)
            .SelectMany(source => source.Targets.Where(to => to is not null && to != source.From).Select(to => (source.From!, to!)))
            .Distinct()
            .ToLookup(edge => edge.Item1, edge => edge.Item2, StringComparer.Ordinal);

        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in Modules(architecture))
        {
            var cycle = FindCycle(start, edges, [], done);
            if (cycle is not null)
            {
                throw new XunitException($"{NoModuleCycles}\nciclo entre módulos: {string.Join(" → ", cycle)}");
            }
        }
    }

    private static List<string>? FindCycle(string module, ILookup<string, string> edges, List<string> path, HashSet<string> done)
    {
        var index = path.IndexOf(module);
        if (index >= 0)
        {
            return [.. path[index..], module];
        }

        if (!done.Add(module))
        {
            return null;
        }

        path.Add(module);
        foreach (var next in edges[module])
        {
            var cycle = FindCycle(next, edges, path, done);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        path.RemoveAt(path.Count - 1);
        return null;
    }

    private static string? ModuleOf(IType type)
    {
        var match = ModuleNamespace().Match(type.Namespace.FullName);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Como <see cref="ForbidDependencies"/>, mas o alvo proibido é casado pelo nome completo do tipo (para liberar
    /// tipos específicos, e não um namespace inteiro).
    /// </summary>
    private static ArchRule<IType> ForbidDependenciesByFullName(string sourceNamespace, string forbiddenFullName, string rule) =>
        Types().That().ResideInNamespaceMatching(sourceNamespace)
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(forbiddenFullName)
            .Because(rule)
            .WithoutRequiringPositiveResults();

    private static ArchRule<IType> ForbidDependencies(string sourceNamespace, string forbiddenNamespace, string rule) =>
        Types().That().ResideInNamespaceMatching(sourceNamespace)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbiddenNamespace)
            .Because(rule)
            .WithoutRequiringPositiveResults();

    private static string Layer(string modulePattern, string layer) =>
        $"^{RootPattern}\\.{modulePattern}\\.{layer}(?:\\..*)?$";

    /// <summary>Nomes dos módulos (<c>Subsolo.Modules.&lt;M&gt;</c>) com tipos nos assemblies avaliados.</summary>
    private static IReadOnlyList<string> Modules(Architecture architecture) =>
        [.. architecture.Types
            .Select(ModuleOf)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"^Subsolo\.Modules\.([^.]+)(?:\.|$)")]
    private static partial Regex ModuleNamespace();
}
