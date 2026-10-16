using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>
/// SPEC-0007 — chama os handlers de um tipo de evento conhecido só em tempo de execução (o tipo vem da outbox ou
/// da inbox), sem reflexão a cada chamada.
/// </summary>
internal abstract class EventDispatcher
{
    private static readonly ConcurrentDictionary<Type, EventDispatcher> DomainDispatchers = new();
    private static readonly ConcurrentDictionary<Type, EventDispatcher> IntegrationDispatchers = new();

    public static EventDispatcher ForDomainEvent(Type eventType) =>
        DomainDispatchers.GetOrAdd(eventType, static type => Create(typeof(DomainEventDispatcher<>), type));

    public static EventDispatcher ForIntegrationEvent(Type eventType) =>
        IntegrationDispatchers.GetOrAdd(eventType, static type => Create(typeof(IntegrationEventDispatcher<>), type));

    /// <summary>Todos os handlers registrados para o evento (de qualquer módulo).</summary>
    public abstract IEnumerable<object> Handlers(IServiceProvider services);

    public abstract Task HandleAsync(object handler, object message, CancellationToken cancellationToken);

    private static EventDispatcher Create(Type dispatcherDefinition, Type eventType) =>
        (EventDispatcher)Activator.CreateInstance(dispatcherDefinition.MakeGenericType(eventType))!;

    private sealed class DomainEventDispatcher<TEvent> : EventDispatcher
        where TEvent : IDomainEvent
    {
        public override IEnumerable<object> Handlers(IServiceProvider services) =>
            services.GetServices<IDomainEventHandler<TEvent>>().Cast<object>();

        public override Task HandleAsync(object handler, object message, CancellationToken cancellationToken) =>
            ((IDomainEventHandler<TEvent>)handler).Handle((TEvent)message, cancellationToken);
    }

    private sealed class IntegrationEventDispatcher<TEvent> : EventDispatcher
        where TEvent : IIntegrationEvent
    {
        public override IEnumerable<object> Handlers(IServiceProvider services) =>
            services.GetServices<IIntegrationEventHandler<TEvent>>().Cast<object>();

        public override Task HandleAsync(object handler, object message, CancellationToken cancellationToken) =>
            ((IIntegrationEventHandler<TEvent>)handler).Handle((TEvent)message, cancellationToken);
    }
}
