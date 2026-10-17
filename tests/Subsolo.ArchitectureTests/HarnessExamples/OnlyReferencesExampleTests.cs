using System.Reflection;
using Shouldly;
using Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.Venues.IntegrationEvents;
using Xunit;

namespace Subsolo.ArchitectureTests.HarnessExamples;

/// <summary>
/// SPEC-0013 IT-06 — <see cref="ModuleArchitectureRules.OnlyReferences"/> segue o ADR-0001: de outro módulo, só o
/// namespace <c>&lt;Outro&gt;.IntegrationEvents</c> pode ser referenciado. O módulo de exemplo que usa
/// <c>Venues.Contracts</c> reprova nomeando a dependência; o que usa <c>Venues.IntegrationEvents</c> passa.
/// </summary>
public sealed class OnlyReferencesExampleTests
{
    private static readonly Assembly Samples = typeof(VenueRegistered).Assembly;

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-06")]
    [Trait("Kind", "Integration")]
    public void ReferencingAnotherModulesContractsNamespaceFails()
    {
        var failure = Should.Throw<Exception>(
            () => ArchitectureRules.ForModule("ContractsConsumer", Samples).OnlyReferences("Venues"));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain("Venues.Contracts.VenueSummary", Case.Sensitive, failure.Message);
    }

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-06")]
    [Trait("Kind", "Integration")]
    public void ReferencingAnotherModulesIntegrationEventsPasses()
    {
        Should.NotThrow(() => ArchitectureRules.ForModule("EventsConsumer", Samples).OnlyReferences("Venues"));
    }
}
