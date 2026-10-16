using System.Reflection;

namespace Subsolo.ArchitectureTests;

/// <summary>
/// SPEC-0007 — regras do ADR-0001 aplicadas a todo módulo (<c>Subsolo.Modules.&lt;M&gt;.*</c>). As camadas são
/// reconhecidas pelos namespaces <c>Subsolo.Modules.&lt;M&gt;.{Domain,Application,Infrastructure,Presentation,
/// IntegrationEvents}</c>. Cada regra falha nomeando a regra (a constante) e os tipos da dependência proibida.
/// </summary>
public static class ModularMonolithRules
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

    /// <summary><c>&lt;M&gt;.IntegrationEvents</c> não depende de nada do Subsolo além de Common.Application.</summary>
    public const string IntegrationEventsDependencies =
        "ADR-0001: o IntegrationEvents do módulo só depende de Common.Application";

    /// <summary>Não há ciclos de dependência entre módulos.</summary>
    public const string NoModuleCycles = "ADR-0001: não há ciclos de dependência entre módulos";

    /// <summary>Todos os assemblies <c>Subsolo.Modules.*</c> ao lado do assembly de teste.</summary>
    public static IReadOnlyList<Assembly> ModuleAssemblies() => throw new NotImplementedException();

    /// <summary>
    /// Avalia a regra <paramref name="rule"/> (uma das constantes desta classe) sobre <paramref name="assemblies"/>
    /// e falha (exceção do ArchUnitNET) com uma mensagem que contém o texto da regra.
    /// </summary>
    public static void Check(string rule, params Assembly[] assemblies) => throw new NotImplementedException();
}
