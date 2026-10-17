namespace Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.Venues.IntegrationEvents;

/// <summary>SPEC-0013 IT-06 — dublê: evento de integração do módulo de exemplo Venues (contrato público entre módulos).</summary>
public sealed record VenueRegistered(Guid Id, string Name);
