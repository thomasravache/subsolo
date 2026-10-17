using Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.Venues.IntegrationEvents;

namespace Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.EventsConsumer.Application;

/// <summary>SPEC-0013 IT-06 — dublê: módulo que referencia só <c>Venues.IntegrationEvents</c> (permitido pelo ADR-0001).</summary>
public static class VenueRegisteredHandler
{
    public static string NameOf(VenueRegistered integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return integrationEvent.Name;
    }
}
