using System.Reflection;

namespace Subsolo.ArchitectureTests;

/// <summary>
/// SPEC-0005 — ponto de entrada para declarar as regras de arquitetura de um módulo (ADR-0001), no layout
/// <c>Subsolo.Modules.&lt;M&gt;.&lt;Camada&gt;</c>. Consumido pela SPEC-0007:
/// <c>ArchitectureRules.ForModule("Events").DomainMustNotDependOn(...).OnlyReferences(...)</c>.
/// </summary>
public static class ArchitectureRules
{
    /// <summary>
    /// Regras do módulo <paramref name="module"/>. As camadas são reconhecidas pelos namespaces
    /// <c>….&lt;module&gt;.Domain</c>, <c>….&lt;module&gt;.Infrastructure</c> etc. Sem <paramref name="assemblies"/>,
    /// avalia os assemblies <c>Subsolo.Modules.&lt;module&gt;.*</c>; com eles, avalia só os assemblies informados.
    /// </summary>
    public static ModuleArchitectureRules ForModule(string module, params Assembly[] assemblies) =>
        throw new NotImplementedException();
}

/// <summary>
/// SPEC-0005 — regras de um módulo. Cada método avalia a regra na hora e falha o teste (exceção do ArchUnitNET)
/// nomeando a dependência proibida; devolve a mesma instância para encadear outras regras.
/// </summary>
public sealed class ModuleArchitectureRules
{
    private ModuleArchitectureRules()
    {
    }

    /// <summary>
    /// A camada Domain do módulo não depende de nenhum dos alvos: uma camada do próprio módulo
    /// (ex.: <c>"Infrastructure"</c>) ou um namespace externo (ex.: <c>"Microsoft.EntityFrameworkCore"</c>).
    /// </summary>
    public ModuleArchitectureRules DomainMustNotDependOn(params string[] targets) =>
        throw new NotImplementedException();

    /// <summary>O módulo só referencia, de outros módulos, os contratos públicos informados.</summary>
    public ModuleArchitectureRules OnlyReferences(params string[] allowedModules) =>
        throw new NotImplementedException();
}
