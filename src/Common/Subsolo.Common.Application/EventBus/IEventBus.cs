namespace Subsolo.Common.Application.EventBus;

/// <summary>SPEC-0007 — barramento em processo: entrega o evento às inboxes dos módulos que o consomem.</summary>
public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;
}
