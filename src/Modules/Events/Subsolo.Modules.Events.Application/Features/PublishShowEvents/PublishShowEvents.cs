using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.PublishShowEvents;

/// <summary>
/// SPEC-0023 — ao processar a outbox, publica <see cref="ShowPublishedIntegrationEvent"/> com o <c>Id</c> do evento de
/// domínio (uma nova tentativa publica o mesmo evento): os avisos e lembretes consomem.
/// </summary>
internal sealed class PublishShowPublished(IEventBus eventBus) : IDomainEventHandler<ShowPublishedDomainEvent>
{
    public Task Handle(ShowPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return eventBus.PublishAsync(
            new ShowPublishedIntegrationEvent(domainEvent.ShowId, domainEvent.City, domainEvent.ProfileIds, domainEvent.StartsAtUtc)
            {
                Id = domainEvent.Id,
                OccurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc),
            },
            cancellationToken);
    }
}

/// <summary>SPEC-0023 — ao processar a outbox, publica <see cref="ShowChangedIntegrationEvent"/> (mesmo <c>Id</c> do evento de domínio).</summary>
internal sealed class PublishShowChanged(IEventBus eventBus) : IDomainEventHandler<ShowChangedDomainEvent>
{
    public Task Handle(ShowChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return eventBus.PublishAsync(
            new ShowChangedIntegrationEvent(domainEvent.ShowId, domainEvent.Status, domainEvent.StartsAtUtc)
            {
                Id = domainEvent.Id,
                OccurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc),
            },
            cancellationToken);
    }
}
