using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.IntegrationEvents;

namespace Subsolo.Modules.Template.Application;

/// <summary>
/// Dublê de teste: ao processar a outbox, transforma o evento de domínio no evento de integração público do módulo.
/// </summary>
public sealed class PublishTemplateItemCreated(IEventBus eventBus, TimeProvider clock, TemplateFaults faults)
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
                Guid.NewGuid(), clock.GetUtcNow().UtcDateTime, domainEvent.ItemId, domainEvent.Name),
            cancellationToken);
    }
}
