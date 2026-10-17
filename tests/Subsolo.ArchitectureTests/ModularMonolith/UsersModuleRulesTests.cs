using System.Reflection;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0014 IT-09 (guarda: passa antes da mudança) — as regras do ADR-0001 (SPEC-0007) valem para o módulo Users sem
/// configuração extra: os cinco assemblies <c>Subsolo.Modules.Users.*</c> estão entre os avaliados, cada regra passa
/// com eles, e nenhum outro módulo referencia <c>Users.Domain</c>, <c>Users.Application</c> nem
/// <c>Users.Infrastructure</c> (só o <c>Users.IntegrationEvents</c>, a superfície pública).
/// </summary>
public sealed class UsersModuleRulesTests
{
    private static readonly string[] UsersAssemblies =
    [
        "Subsolo.Modules.Users.Application",
        "Subsolo.Modules.Users.Domain",
        "Subsolo.Modules.Users.Infrastructure",
        "Subsolo.Modules.Users.IntegrationEvents",
        "Subsolo.Modules.Users.Presentation",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-09")]
    [Trait("Kind", "Integration")]
    public void UsersAssembliesAreEvaluatedByTheRules()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var name in UsersAssemblies)
        {
            names.ShouldContain(name);
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0014:IT-09")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void UsersModuleFollowsTheRule(string rule)
    {
        var modules = ModularMonolithRules.ModuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(rule, modules));
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-09")]
    [Trait("Kind", "Integration")]
    public void NoOtherModuleReferencesUsersInternals()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        var architecture = new ArchLoader().LoadAssemblies(modules).Build();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Users(?:\.|$))[^.]+(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Users\.(?:Domain|Application|Infrastructure)(?:\..*)?$")
            .Because("ADR-0001: outro módulo só usa o Users pelo Users.IntegrationEvents")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }
}
