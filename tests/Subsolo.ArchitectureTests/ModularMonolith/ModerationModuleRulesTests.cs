using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0033 IT-06 — as regras do ADR-0001 (SPEC-0007) valem para o módulo Moderation sem configuração extra: os cinco
/// assemblies <c>Subsolo.Modules.Moderation.*</c> estão entre os avaliados e cada regra passa com eles (sem ciclos);
/// nenhum outro módulo referencia o Moderation (o Events não o conhece: o <c>ShowHiddenByModeration</c> mora no contrato do
/// Events, padrão do consumidor); o Moderation depende só do Users e do Events, e só pelo IntegrationEvents deles (valida o
/// show pelo <c>IEventsCatalog</c>, publica o <c>ShowHiddenByModerationIntegrationEvent</c>, consome o <c>UserDeleted</c> e
/// registra a seção do export, <c>IPersonalDataSection</c>); e o Events consome o evento.
/// </summary>
public sealed class ModerationModuleRulesTests
{
    private const string ShowHiddenEvent = "Subsolo.Modules.Events.IntegrationEvents.ShowHiddenByModerationIntegrationEvent";

    private static readonly string[] ModerationAssemblies =
    [
        "Subsolo.Modules.Moderation.Application",
        "Subsolo.Modules.Moderation.Domain",
        "Subsolo.Modules.Moderation.Infrastructure",
        "Subsolo.Modules.Moderation.IntegrationEvents",
        "Subsolo.Modules.Moderation.Presentation",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    public void ModerationAssembliesAreEvaluatedByTheRules()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var name in ModerationAssemblies)
        {
            names.ShouldContain(name);
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void ModerationModuleFollowsTheRule(string rule)
    {
        var modules = ModularMonolithRules.ModuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(rule, modules));
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    public void NoOtherModuleDependsOnModeration()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Moderation(?:\.|$))[^.]+(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Moderation(?:\..*)?$")
            .Because("SPEC-0033 §4: Moderation → Users, Events; o Events não referencia o Moderation (o evento é do contrato do Events)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    public void ModerationUsesOtherModulesOnlyThroughTheirIntegrationEvents()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Moderation(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Moderation(?:\.|$))[^.]+\.(?:Domain|Application|Infrastructure|Presentation)(?:\..*)?$")
            .Because("ADR-0001: o Moderation não lê o banco de outro módulo (só as interfaces públicas)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    public void ModerationDependsOnlyOnUsersAndEvents()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Moderation(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Moderation(?:\.|$)|Users(?:\.|$)|Events(?:\.|$))[^.]+(?:\..*)?$")
            .Because("SPEC-0033 §4: a dependência do Moderation é só Users e Events")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData("Subsolo.Modules.Events.IntegrationEvents.IEventsCatalog", "o show é validado pelo IEventsCatalog")]
    [InlineData(ShowHiddenEvent, "ocultar publica o ShowHiddenByModeration")]
    [InlineData("Subsolo.Modules.Users.IntegrationEvents.UserDeletedIntegrationEvent", "as denúncias ficam sem autor com a conta (UserDeleted)")]
    [InlineData("Subsolo.Modules.Users.IntegrationEvents.IPersonalDataSection", "as denúncias entram no export (IPersonalDataSection)")]
    public void ModerationUsesThePublicContractOf(string type, string reason)
    {
        var architecture = Load();

        var uses = TypesOf(architecture, "Subsolo.Modules.Moderation.")
            .Any(candidate => candidate.Dependencies.Any(dependency => dependency.Target?.FullName == type));

        uses.ShouldBeTrue($"SPEC-0033: {reason} — nenhum tipo do Moderation usa {type}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-06")]
    [Trait("Kind", "Integration")]
    public void TheShowHiddenEventLivesInTheEventsContractAndEventsConsumesIt()
    {
        var types = ModularMonolithRules.ModuleAssemblies().SelectMany(assembly => assembly.GetExportedTypes()).ToList();

        var hidden = types.Where(type => type.FullName == ShowHiddenEvent)
            .ShouldHaveSingleItem("ShowHiddenByModerationIntegrationEvent não encontrado (ou duplicado) no Events.IntegrationEvents");
        hidden.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        types.ShouldNotContain(
            type => type.Name == "ShowHiddenByModerationIntegrationEvent" && type.FullName != ShowHiddenEvent,
            "o evento de ocultar também foi declarado fora do contrato do Events");

        var consumed = TypesOf(Load(), "Subsolo.Modules.Events.")
            .Any(candidate => candidate.Dependencies.Any(dependency => dependency.Target?.FullName == ShowHiddenEvent));
        consumed.ShouldBeTrue("SPEC-0033: o Events não consome o ShowHiddenByModeration (nenhum tipo fora do contrato o usa)");
    }

    private static Architecture Load() =>
        new ArchLoader().LoadAssemblies([.. ModularMonolithRules.ModuleAssemblies()]).Build();

    /// <summary>Os tipos do módulo <paramref name="prefix"/> fora do IntegrationEvents dele.</summary>
    private static IEnumerable<IType> TypesOf(Architecture architecture, string prefix) =>
        architecture.Types
            .Where(candidate => candidate.Namespace?.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
            .Where(candidate => !candidate.FullName.Contains("IntegrationEvents", StringComparison.Ordinal));
}
