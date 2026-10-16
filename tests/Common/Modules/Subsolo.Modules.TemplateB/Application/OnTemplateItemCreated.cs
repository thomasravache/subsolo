using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Template.IntegrationEvents;

namespace Subsolo.Modules.TemplateB.Application;

/// <summary>Dublê de teste: handler do TemplateB para o evento de integração do Template (via inbox).</summary>
public sealed class OnTemplateItemCreated(TemplateBProbe probe)
    : IIntegrationEventHandler<TemplateItemCreatedIntegrationEvent>
{
    public Task Handle(TemplateItemCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        probe.Record(integrationEvent);
        return Task.CompletedTask;
    }
}
