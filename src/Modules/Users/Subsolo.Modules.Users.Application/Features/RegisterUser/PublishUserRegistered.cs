using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.RegisterUser;

/// <summary>
/// SPEC-0014 — ao processar a outbox, publica <see cref="UserRegisteredIntegrationEvent"/> (só o id e a data: o e-mail
/// não sai do módulo), com o <c>Id</c> do evento de domínio (uma nova tentativa publica o mesmo evento).
/// </summary>
internal sealed class PublishUserRegistered(IEventBus eventBus) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var occurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc);
        return eventBus.PublishAsync(
            new UserRegisteredIntegrationEvent(domainEvent.UserId, new DateTimeOffset(occurredOnUtc))
            {
                Id = domainEvent.Id,
                OccurredOnUtc = occurredOnUtc,
            },
            cancellationToken);
    }
}
