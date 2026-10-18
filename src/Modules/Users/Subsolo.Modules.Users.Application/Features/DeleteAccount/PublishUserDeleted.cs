using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.DeleteAccount;

/// <summary>
/// SPEC-0019 — ao processar a outbox, publica <see cref="UserDeletedIntegrationEvent"/> (só o id e a data: o e-mail não
/// sai do módulo), com o <c>Id</c> do evento de domínio (uma nova tentativa publica o mesmo evento).
/// </summary>
internal sealed class PublishUserDeleted(IEventBus eventBus) : IDomainEventHandler<UserDeletedDomainEvent>
{
    public Task Handle(UserDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var occurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc);
        return eventBus.PublishAsync(
            new UserDeletedIntegrationEvent(domainEvent.UserId, new DateTimeOffset(occurredOnUtc))
            {
                Id = domainEvent.Id,
                OccurredOnUtc = occurredOnUtc,
            },
            cancellationToken);
    }
}
