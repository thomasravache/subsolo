using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Template.IntegrationEvents;

/// <summary>Dublê de teste: evento de integração publicado pelo módulo Template quando um item é criado.</summary>
public sealed record TemplateItemCreatedIntegrationEvent(Guid Id, DateTime OccurredOnUtc, Guid ItemId, string Name)
    : IIntegrationEvent;
