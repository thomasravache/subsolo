using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.IntegrationEvents;

namespace Subsolo.Modules.Template.Application;

/// <summary>
/// Dublê de teste: ao processar a outbox, transforma o evento de domínio no evento de integração público do módulo,
/// reaproveitando o <c>Id</c> e o <c>OccurredOnUtc</c> do evento de domínio (uma nova tentativa publica o mesmo
/// evento, que a inbox do outro módulo descarta). As falhas injetadas pelo <see cref="TemplateFaults"/> acontecem
/// antes ou logo depois da publicação.
/// </summary>
public sealed class PublishTemplateItemCreated(IEventBus eventBus, TemplateFaults faults)
    : IDomainEventHandler<TemplateItemCreatedDomainEvent>
{
    public async Task Handle(TemplateItemCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (faults.RegisterAttemptAndShouldFail())
        {
            throw new InvalidOperationException("Falha injetada pelo teste (retry da outbox).");
        }

        await eventBus.PublishAsync(
            new TemplateItemCreatedIntegrationEvent(
                domainEvent.Id, domainEvent.OccurredOnUtc, domainEvent.ItemId, domainEvent.Name),
            cancellationToken);

        if (faults.ShouldFailAfterPublish())
        {
            throw new InvalidOperationException("Falha injetada pelo teste depois de publicar (retry da outbox).");
        }
    }
}
