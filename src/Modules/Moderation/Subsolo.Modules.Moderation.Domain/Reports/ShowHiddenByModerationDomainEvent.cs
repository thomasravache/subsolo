using Subsolo.Common.Domain;

namespace Subsolo.Modules.Moderation.Domain.Reports;

/// <summary>
/// SPEC-0033 — o admin ocultou o show (vira <c>ShowHiddenByModerationIntegrationEvent</c>, do contrato do Events, pela
/// outbox). Levantado uma vez por decisão, não por denúncia.
/// </summary>
public sealed record ShowHiddenByModerationDomainEvent(Guid ShowId, DateTimeOffset HiddenAtUtc) : DomainEvent;
