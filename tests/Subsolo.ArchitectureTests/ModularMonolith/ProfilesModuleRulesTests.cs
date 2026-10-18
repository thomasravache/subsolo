using System.Reflection;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Subsolo.Modules.CollectionSampleA.Application;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0021 IT-08 — as regras do ADR-0001 (SPEC-0007) valem para o módulo Profiles sem configuração extra: os cinco
/// assemblies <c>Subsolo.Modules.Profiles.*</c> estão entre os avaliados, cada regra passa com eles, nenhum outro módulo
/// referencia <c>Profiles.Domain</c>, <c>Profiles.Application</c> nem <c>Profiles.Infrastructure</c>, o Profiles não
/// usa nada do Events nem do Users além do IntegrationEvents deles (não lê o banco de outro módulo), e as interfaces
/// públicas <c>IEventsCatalog</c>, <c>IProfilesApi</c> e <c>IUsersDirectory</c> e o evento <c>ArtistRoleGranted</c> moram no
/// IntegrationEvents (Emenda v2: o Users não referencia o Profiles). Guarda (passa antes da mudança para
/// os módulos existentes): nenhum outro módulo referencia as internas do Profiles. §7.6 (herança da revisão da
/// SPEC-0017): a regra de ciclos tolera dependências que o ArchUnitNET não resolve (expressões de coleção).
/// </summary>
public sealed class ProfilesModuleRulesTests
{
    private static readonly string[] ProfilesAssemblies =
    [
        "Subsolo.Modules.Profiles.Application",
        "Subsolo.Modules.Profiles.Domain",
        "Subsolo.Modules.Profiles.Infrastructure",
        "Subsolo.Modules.Profiles.IntegrationEvents",
        "Subsolo.Modules.Profiles.Presentation",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void ProfilesAssembliesAreEvaluatedByTheRules()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var name in ProfilesAssemblies)
        {
            names.ShouldContain(name);
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void ProfilesModuleFollowsTheRule(string rule)
    {
        var modules = ModularMonolithRules.ModuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(rule, modules));
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void NoOtherModuleReferencesProfilesInternals()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        var architecture = new ArchLoader().LoadAssemblies(modules).Build();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Profiles(?:\.|$))[^.]+(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Profiles\.(?:Domain|Application|Infrastructure|Presentation)(?:\..*)?$")
            .Because("ADR-0001: outro módulo só usa o Profiles pelo Profiles.IntegrationEvents")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void ProfilesUsesEventsAndUsersOnlyThroughTheirIntegrationEvents()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        var architecture = new ArchLoader().LoadAssemblies(modules).Build();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Profiles(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?:Events|Users)\.(?:Domain|Application|Infrastructure|Presentation)(?:\..*)?$")
            .Because("ADR-0001: o Profiles não lê o banco do Events nem do Users (só as interfaces públicas)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
        var usesCatalog = architecture.Types
            .Where(type => type.Namespace?.FullName?.StartsWith("Subsolo.Modules.Profiles.", StringComparison.Ordinal) == true)
            .Any(type => type.Dependencies.Any(dependency =>
                dependency.Target?.FullName == "Subsolo.Modules.Events.IntegrationEvents.IEventsCatalog"));
        usesCatalog.ShouldBeTrue("SPEC-0021: cidade e estilo são validados pelo IEventsCatalog");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void UsersDoesNotReferenceProfiles()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        var architecture = new ArchLoader().LoadAssemblies(modules).Build();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Users(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Profiles(?:\..*)?$")
            .Because("ADR-0001 (Emenda v2 da SPEC-0021): a dependência é só Profiles → Users; o evento do papel artist é do contrato do Users")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void ThePublicInterfacesLiveInTheIntegrationEvents()
    {
        var types = ModularMonolithRules.ModuleAssemblies().SelectMany(assembly => assembly.GetExportedTypes()).ToList();

        var catalog = types.Where(type => type.Name == "IEventsCatalog").ShouldHaveSingleItem("IEventsCatalog não encontrado (ou duplicado)");
        catalog.IsInterface.ShouldBeTrue();
        catalog.Namespace.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        catalog.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        var profiles = types.Where(type => type.Name == "IProfilesApi").ShouldHaveSingleItem("IProfilesApi não encontrado (ou duplicado)");
        profiles.IsInterface.ShouldBeTrue();
        profiles.Namespace.ShouldBe("Subsolo.Modules.Profiles.IntegrationEvents");
        profiles.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Profiles.IntegrationEvents");
        var directory = types.Where(type => type.Name == "IUsersDirectory").ShouldHaveSingleItem("IUsersDirectory não encontrado (ou duplicado)");
        directory.Namespace.ShouldBe("Subsolo.Modules.Users.IntegrationEvents");
        types.Where(type => type.Name == "ArtistRoleGrantedIntegrationEvent").ShouldHaveSingleItem()
            .Namespace.ShouldBe("Subsolo.Modules.Users.IntegrationEvents");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void TheCycleRuleToleratesDependenciesArchUnitCannotResolve()
    {
        var sample = typeof(CollectionExpressionUser).Assembly;

        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.NoModuleCycles, sample));
        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.ModuleBoundaries, sample));
    }
}
