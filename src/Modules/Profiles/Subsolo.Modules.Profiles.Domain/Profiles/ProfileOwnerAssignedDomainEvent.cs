using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>
/// SPEC-0021 — o perfil ganhou dono (vira <c>ProfileOwnerAssignedIntegrationEvent</c> pela outbox). Só ids.
/// </summary>
public sealed record ProfileOwnerAssignedDomainEvent(Guid ProfileId, Guid UserId) : DomainEvent;
