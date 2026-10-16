using System.Collections.Concurrent;
using Subsolo.Modules.Template.IntegrationEvents;

namespace Subsolo.Modules.TemplateB.Application;

/// <summary>Dublê de teste: registra cada execução do handler do TemplateB. Singleton.</summary>
public sealed class TemplateBProbe
{
    private readonly ConcurrentQueue<TemplateItemCreatedIntegrationEvent> _received = new();

    public IReadOnlyCollection<TemplateItemCreatedIntegrationEvent> Received => [.. _received];

    internal void Record(TemplateItemCreatedIntegrationEvent integrationEvent) => _received.Enqueue(integrationEvent);
}
