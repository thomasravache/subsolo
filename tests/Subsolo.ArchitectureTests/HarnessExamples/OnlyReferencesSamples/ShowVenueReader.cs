using Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.Venues.Contracts;

namespace Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.ContractsConsumer.Application;

/// <summary>SPEC-0013 IT-06 — dublê: módulo que referencia <c>Venues.Contracts</c> (viola o ADR-0001).</summary>
public static class ShowVenueReader
{
    public static string NameOf(VenueSummary venue)
    {
        ArgumentNullException.ThrowIfNull(venue);
        return venue.Name;
    }
}
