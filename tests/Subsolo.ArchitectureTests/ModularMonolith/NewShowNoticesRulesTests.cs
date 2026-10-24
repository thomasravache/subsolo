using System.Reflection;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0031 IT-06 (ADR-0001, ADR-0014) — com o aviso de show novo e o resumo diário no Engagement:
/// <list type="bullet">
/// <item>o Engagement só depende de outros módulos entre Users, Events, Profiles e Notifications, e só pelo
/// IntegrationEvents deles;</item>
/// <item>o Notifications não referencia o Engagement (o Engagement decide quem recebe; o Notifications entrega);</item>
/// <item>o Engagement consome o <c>ShowPublishedIntegrationEvent</c> (um <c>IIntegrationEventHandler</c> no
/// Application), lê o show por <c>IEventsCatalog.GetShowSummaryAsync</c> e enfileira por
/// <c>INotificationsApi.EnqueueAsync</c> (o contrato, sem atalho).</item>
/// </list>
/// </summary>
public sealed class NewShowNoticesRulesTests
{
    private const string EngagementApplication = "Subsolo.Modules.Engagement.Application";

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-06")]
    [Trait("Kind", "Integration")]
    public void EngagementDependsOnlyOnUsersEventsProfilesAndNotificationsContracts()
    {
        var architecture = Load();

        var otherModules = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!(?:Engagement|Users|Events|Profiles|Notifications)(?:\.|$))[^.]+(?:\..*)?$")
            .Because("ADR-0001: Engagement → Users, Events, Profiles, Notifications (nenhum outro módulo)")
            .WithoutRequiringPositiveResults();
        var internals = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?:Users|Events|Profiles|Notifications)\.(?!IntegrationEvents(?:\.|$)).*$")
            .Because("ADR-0001: os outros módulos só pelo contrato (IntegrationEvents)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => otherModules.Check(architecture));
        Should.NotThrow(() => internals.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-06")]
    [Trait("Kind", "Integration")]
    public void NotificationsDoesNotReferenceEngagement()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Notifications(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.Engagement(?:\..*)?$")
            .Because("ADR-0014 (2026-10-18): o Notifications entrega e não conhece quem segue o quê")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
        foreach (var assembly in ModularMonolithRules.ModuleAssemblies()
            .Where(assembly => assembly.GetName().Name!.StartsWith("Subsolo.Modules.Notifications.", StringComparison.Ordinal)))
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name)
                .ShouldNotContain(name => name!.StartsWith("Subsolo.Modules.Engagement", StringComparison.Ordinal),
                    $"{assembly.GetName().Name} referencia o Engagement");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-06")]
    [Trait("Kind", "Integration")]
    public void EngagementConsumesShowPublishedThroughTheInbox()
    {
        var handlers = MemberReferences.Module(EngagementApplication).GetTypes()
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition().FullName == "Subsolo.Common.Application.EventBus.IIntegrationEventHandler`1"
                && contract.GetGenericArguments()[0].FullName == "Subsolo.Modules.Events.IntegrationEvents.ShowPublishedIntegrationEvent"))
            .ToList();

        handlers.ShouldNotBeEmpty("o Engagement não consome o ShowPublishedIntegrationEvent (IIntegrationEventHandler)");
    }

    [Theory]
    [Trait("Spec", "SPEC-0031:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData("Subsolo.Modules.Events.IntegrationEvents", "IEventsCatalog", "GetShowSummaryAsync", "o resumo do show vem do contrato do Events")]
    [InlineData("Subsolo.Modules.Notifications.IntegrationEvents", "INotificationsApi", "EnqueueAsync", "os avisos saem pelo contrato do Notifications")]
    public void EngagementCallsTheContract(string typeNamespace, string typeName, string method, string reason)
    {
        var calls = MemberReferences.CalledOn(MemberReferences.Module(EngagementApplication), typeNamespace, typeName);

        calls.ShouldContain(method, $"SPEC-0031: o Engagement não chama {typeName}.{method} ({reason})");
    }

    private static ArchUnitNET.Domain.Architecture Load()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        return new ArchLoader().LoadAssemblies(modules).Build();
    }
}
