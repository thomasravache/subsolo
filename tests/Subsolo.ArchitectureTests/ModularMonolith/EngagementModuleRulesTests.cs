using System.Reflection;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0027 IT-06 — as regras do ADR-0001 (SPEC-0007) valem para o módulo Engagement sem configuração extra: os cinco
/// assemblies <c>Subsolo.Modules.Engagement.*</c> estão entre os avaliados e cada regra passa com eles (sem ciclos);
/// nenhum outro módulo referencia as internas do Engagement; o Engagement usa o Users, o Events e o Profiles só pelo
/// IntegrationEvents deles (valida a cidade pelo <c>IEventsCatalog</c> e o perfil pelo <c>IProfilesApi</c>), consome o
/// <c>UserDeleted</c> e registra a seção do export (<c>IPersonalDataSection</c>); Users, Events e Profiles não dependem do
/// Engagement (guarda para os módulos existentes); e o <c>IEngagementApi</c> mora no Engagement.IntegrationEvents.
/// </summary>
public sealed class EngagementModuleRulesTests
{
    private static readonly string[] EngagementAssemblies =
    [
        "Subsolo.Modules.Engagement.Application",
        "Subsolo.Modules.Engagement.Domain",
        "Subsolo.Modules.Engagement.Infrastructure",
        "Subsolo.Modules.Engagement.IntegrationEvents",
        "Subsolo.Modules.Engagement.Presentation",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    public void EngagementAssembliesAreEvaluatedByTheRules()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var name in EngagementAssemblies)
        {
            names.ShouldContain(name);
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void EngagementModuleFollowsTheRule(string rule)
    {
        var modules = ModularMonolithRules.ModuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(rule, modules));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    public void NoOtherModuleReferencesEngagementInternals()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Engagement(?:\.|$))[^.]+(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement\.(?:Domain|Application|Infrastructure|Presentation)(?:\..*)?$")
            .Because("ADR-0001: outro módulo só usa o Engagement pelo Engagement.IntegrationEvents")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    public void UsersEventsAndProfilesDoNotDependOnEngagement()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?:Users|Events|Profiles)(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement(?:\..*)?$")
            .Because("SPEC-0027 §4: a dependência é só Engagement → Users, Events, Profiles (sem ciclos)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    public void EngagementUsesOtherModulesOnlyThroughTheirIntegrationEvents()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Engagement(?:\.|$))[^.]+\.(?:Domain|Application|Infrastructure|Presentation)(?:\..*)?$")
            .Because("ADR-0001: o Engagement não lê o banco de outro módulo (só as interfaces públicas)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Theory]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData("Subsolo.Modules.Events.IntegrationEvents.IEventsCatalog", "a cidade é validada pelo IEventsCatalog")]
    [InlineData("Subsolo.Modules.Profiles.IntegrationEvents.IProfilesApi", "o perfil é validado pelo IProfilesApi")]
    [InlineData("Subsolo.Modules.Users.IntegrationEvents.UserDeletedIntegrationEvent", "os follows somem com a conta (UserDeleted)")]
    [InlineData("Subsolo.Modules.Users.IntegrationEvents.IPersonalDataSection", "os follows entram no export (IPersonalDataSection)")]
    public void EngagementUsesThePublicContractOf(string type, string reason)
    {
        var architecture = Load();

        var uses = architecture.Types
            .Where(candidate => candidate.Namespace?.FullName?.StartsWith("Subsolo.Modules.Engagement.", StringComparison.Ordinal) == true)
            .Where(candidate => !candidate.FullName.Contains("IntegrationEvents", StringComparison.Ordinal))
            .Any(candidate => candidate.Dependencies.Any(dependency => dependency.Target?.FullName == type));

        uses.ShouldBeTrue($"SPEC-0027: {reason}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-06")]
    [Trait("Kind", "Integration")]
    public void TheEngagementApiLivesInTheIntegrationEvents()
    {
        var types = ModularMonolithRules.ModuleAssemblies().SelectMany(assembly => assembly.GetExportedTypes()).ToList();

        var api = types.Where(type => type.Name == "IEngagementApi").ShouldHaveSingleItem("IEngagementApi não encontrado (ou duplicado)");
        api.IsInterface.ShouldBeTrue();
        api.Namespace.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
        api.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
    }

    private static ArchUnitNET.Domain.Architecture Load()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        return new ArchLoader().LoadAssemblies(modules).Build();
    }
}
