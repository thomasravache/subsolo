using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Subsolo.ArchitectureTests;

/// <summary>
/// SPEC-0005 — ponto de entrada para declarar as regras de arquitetura de um módulo (ADR-0001), no layout
/// <c>Subsolo.Modules.&lt;M&gt;.&lt;Camada&gt;</c>. Consumido pela SPEC-0007:
/// <c>ArchitectureRules.ForModule("Events").DomainMustNotDependOn(...).OnlyReferences(...)</c>.
/// </summary>
public static class ArchitectureRules
{
    /// <summary>Prefixo dos assemblies e namespaces dos módulos reais (ADR-0001).</summary>
    public const string ModulesRoot = "Subsolo.Modules";

    /// <summary>
    /// Regras do módulo <paramref name="module"/>. As camadas são reconhecidas pelos namespaces
    /// <c>….&lt;module&gt;.Domain</c>, <c>….&lt;module&gt;.Infrastructure</c> etc. Sem <paramref name="assemblies"/>,
    /// avalia os assemblies <c>Subsolo.Modules.&lt;module&gt;.*</c>; com eles, avalia só os assemblies informados.
    /// </summary>
    public static ModuleArchitectureRules ForModule(string module, params Assembly[] assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        if (module.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Nome de módulo sem ponto, ex.: \"Events\" (recebido: \"{module}\").", nameof(module));
        }

        var targets = assemblies is { Length: > 0 } ? assemblies : ModuleAssemblies(module);
        var architecture = new ArchLoader().LoadAssemblies(targets).Build();
        return new ModuleArchitectureRules(module, architecture);
    }

    /// <summary>Assemblies <c>Subsolo.Modules.&lt;module&gt;.*</c> ao lado do assembly de teste.</summary>
    private static Assembly[] ModuleAssemblies(string module)
    {
        var prefix = $"{ModulesRoot}.{module}";
        var found = Directory
            .EnumerateFiles(AppContext.BaseDirectory, $"{prefix}*.dll")
            .Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .ToArray();

        return found.Length > 0
            ? found
            : throw new InvalidOperationException(
                $"Nenhum assembly {prefix}* em {AppContext.BaseDirectory}: referencie os projetos do módulo " +
                "no projeto de testes de arquitetura ou informe os assemblies em ForModule.");
    }
}

/// <summary>
/// SPEC-0005 — regras de um módulo. Cada método avalia a regra na hora e falha o teste (exceção do ArchUnitNET)
/// nomeando a dependência proibida; devolve a mesma instância para encadear outras regras.
/// </summary>
public sealed class ModuleArchitectureRules
{
    private readonly string _module;
    private readonly Architecture _architecture;

    internal ModuleArchitectureRules(string module, Architecture architecture)
    {
        _module = module;
        _architecture = architecture;
    }

    /// <summary>
    /// A camada Domain do módulo não depende de nenhum dos alvos: uma camada do próprio módulo
    /// (ex.: <c>"Infrastructure"</c>) ou um namespace externo (ex.: <c>"Microsoft.EntityFrameworkCore"</c>).
    /// Alvo sem ponto é camada do módulo; com ponto, namespace externo (ele e os sub-namespaces).
    /// </summary>
    public ModuleArchitectureRules DomainMustNotDependOn(params string[] targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Length == 0)
        {
            throw new ArgumentException("Informe ao menos um alvo proibido.", nameof(targets));
        }

        var forbidden = string.Join('|', targets.Select(TargetPattern));
        Types().That().ResideInNamespaceMatching(LayerPattern("Domain"))
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching($"^(?:{forbidden})$"))
            .Because($"o Domain do módulo {_module} não depende de {string.Join(", ", targets)} (ADR-0001)")
            .Check(_architecture);
        return this;
    }

    /// <summary>
    /// O módulo só referencia, de outros módulos, o contrato público (<c>&lt;Outro&gt;.IntegrationEvents</c>, ADR-0001)
    /// dos módulos informados; qualquer outro namespace de outro módulo reprova.
    /// </summary>
    public ModuleArchitectureRules OnlyReferences(params string[] allowedModules)
    {
        ArgumentNullException.ThrowIfNull(allowedModules);
        var root = Regex.Escape(ModulesRootNamespace());
        var module = Regex.Escape(_module);
        var allowedContracts = allowedModules.Length == 0
            ? string.Empty
            : $"(?!(?:{string.Join('|', allowedModules.Select(Regex.Escape))})\\.IntegrationEvents(?:\\.|$))";
        var otherModules = $"^{root}\\.(?!{module}(?:\\.|$)){allowedContracts}[^.]+(?:\\..*)?$";

        Types().That().ResideInNamespaceMatching($"^{root}\\.{module}(?:\\..*)?$")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(otherModules))
            .Because(allowedModules.Length == 0
                ? $"o módulo {_module} não referencia outros módulos (ADR-0001)"
                : $"o módulo {_module} só referencia os IntegrationEvents de {string.Join(", ", allowedModules)} (ADR-0001)")
            .Check(_architecture);
        return this;
    }

    /// <summary>Namespace de uma camada do módulo, em qualquer prefixo (<c>….&lt;M&gt;.&lt;Camada&gt;</c>).</summary>
    private string LayerPattern(string layer) =>
        $"^(?:.+\\.)?{Regex.Escape(_module)}\\.{Regex.Escape(layer)}(?:\\..*)?$";

    private string TargetPattern(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return target.Contains('.', StringComparison.Ordinal)
            ? $"{Regex.Escape(target)}(?:\\..*)?"
            : $"(?:.+\\.)?{Regex.Escape(_module)}\\.{Regex.Escape(target)}(?:\\..*)?";
    }

    /// <summary>
    /// Namespace que contém os módulos: o prefixo antes de <c>.&lt;M&gt;</c> nos tipos do módulo
    /// (<c>Subsolo.Modules</c> nos módulos reais).
    /// </summary>
    private string ModulesRootNamespace()
    {
        var marker = $".{_module}";
        var roots = _architecture.Types
            .Select(type => RootOf(type.Namespace.FullName, marker))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return roots.Count switch
        {
            1 => roots[0],
            0 => throw new InvalidOperationException($"Nenhum tipo do módulo {_module} nos assemblies avaliados."),
            _ => roots.Contains(ArchitectureRules.ModulesRoot, StringComparer.Ordinal)
                ? ArchitectureRules.ModulesRoot
                : throw new InvalidOperationException(
                    $"Módulo {_module} encontrado em mais de um namespace: {string.Join(", ", roots)}."),
        };
    }

    private static string? RootOf(string ns, string marker)
    {
        if (ns.EndsWith(marker, StringComparison.Ordinal))
        {
            return ns[..^marker.Length];
        }

        var index = ns.IndexOf(marker + ".", StringComparison.Ordinal);
        return index > 0 ? ns[..index] : null;
    }
}
