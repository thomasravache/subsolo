using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.HideShow;

/// <summary>
/// SPEC-0033 — ao processar a outbox, publica <see cref="ShowHiddenByModerationIntegrationEvent"/> (contrato do Events,
/// padrão do consumidor) com o <c>Id</c> do evento de domínio (uma nova tentativa publica o mesmo evento).
/// </summary>
internal sealed class PublishShowHidden(IEventBus eventBus) : IDomainEventHandler<ShowHiddenByModerationDomainEvent>
{
    public Task Handle(ShowHiddenByModerationDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return eventBus.PublishAsync(
            new ShowHiddenByModerationIntegrationEvent(domainEvent.ShowId, domainEvent.HiddenAtUtc)
            {
                Id = domainEvent.Id,
                OccurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc),
            },
            cancellationToken);
    }
}
