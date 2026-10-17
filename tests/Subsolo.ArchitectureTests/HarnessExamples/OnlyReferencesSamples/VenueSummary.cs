namespace Subsolo.ArchitectureTests.HarnessExamples.OnlyReferencesSamples.Venues.Contracts;

/// <summary>
/// SPEC-0013 IT-06 — dublê: tipo público do módulo de exemplo Venues fora de <c>IntegrationEvents</c>; outros módulos
/// não podem referenciá-lo (ADR-0001).
/// </summary>
public sealed record VenueSummary(Guid Id, string Name);
