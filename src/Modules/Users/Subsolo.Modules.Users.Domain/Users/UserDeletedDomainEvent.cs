using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Domain.Users;

/// <summary>
/// SPEC-0019 — a conta foi excluída pelo titular (vira <c>UserDeletedIntegrationEvent</c> pela outbox). Leva só o id:
/// o e-mail não sai do módulo.
/// </summary>
public sealed record UserDeletedDomainEvent(Guid UserId) : DomainEvent;
