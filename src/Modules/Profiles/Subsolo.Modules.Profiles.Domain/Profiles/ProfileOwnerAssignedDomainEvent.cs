using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>
/// SPEC-0021 — o perfil ganhou dono (vira <c>ArtistRoleGrantedIntegrationEvent</c>, do contrato do Users, pela outbox — Emenda v2). Só ids.
/// </summary>
public sealed record ProfileOwnerAssignedDomainEvent(Guid ProfileId, Guid UserId) : DomainEvent;
