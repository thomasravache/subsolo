using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Domain.Users;

/// <summary>SPEC-0014 — primeira entrada de um usuário (vira <c>UserRegisteredIntegrationEvent</c> pela outbox).</summary>
public sealed record UserRegisteredDomainEvent(Guid UserId) : DomainEvent;
