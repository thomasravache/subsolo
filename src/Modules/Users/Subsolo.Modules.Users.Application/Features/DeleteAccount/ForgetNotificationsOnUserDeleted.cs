using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.DeleteAccount;

/// <summary>
/// SPEC-0030 (ADR-0008) — ao processar a outbox da exclusão da conta, pede ao Notifications que apague as inscrições de
/// push, as preferências e as entregas do usuário (<see cref="INotificationsApi.ForgetUserAsync"/>, idempotente; uma
/// falha agenda nova tentativa da outbox).
/// </summary>
internal sealed class ForgetNotificationsOnUserDeleted(INotificationsApi notifications)
    : IDomainEventHandler<UserDeletedDomainEvent>
{
    public Task Handle(UserDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifications.ForgetUserAsync(domainEvent.UserId, cancellationToken);
    }
}
