using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Domain.Shows;

/// <summary>
/// SPEC-0023 — show publicado (vira <c>ShowPublishedIntegrationEvent</c> pela outbox): cidade, início em UTC e os
/// perfis envolvidos (organizador + casa com perfil + bandas do line-up com perfil).
/// </summary>
public sealed record ShowPublishedDomainEvent(Guid ShowId, string City, IReadOnlyList<Guid> ProfileIds, DateTimeOffset StartsAtUtc)
    : DomainEvent;

/// <summary>SPEC-0023 — show editado ou com a situação mudada (vira <c>ShowChangedIntegrationEvent</c> pela outbox).</summary>
public sealed record ShowChangedDomainEvent(Guid ShowId, string Status, DateTimeOffset StartsAtUtc) : DomainEvent;
