using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Outbox;

/// <summary>
/// SPEC-0007 — job Quartz <c>ProcessOutbox</c> do módulo: entrega os eventos de domínio da outbox aos
/// <c>IDomainEventHandler</c> do módulo (que publicam os eventos de integração pelo <c>IEventBus</c>).
/// </summary>
internal sealed class ProcessOutboxJob<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ModuleDescriptor> modules,
    TimeProvider clock,
    ILogger<ProcessOutboxJob<TDbContext>> logger)
    : ProcessMessagesJob<TDbContext, OutboxMessage, OutboxMessageConsumer>(scopeFactory, modules, clock, logger)
    where TDbContext : ModuleDbContext
{
    protected override Type? ResolveType(ModuleDescriptor module, string typeName) =>
        module.DomainEventTypes.GetValueOrDefault(typeName);

    protected override EventDispatcher Dispatcher(Type eventType) => EventDispatcher.ForDomainEvent(eventType);

    protected override OutboxMessageConsumer Consumer(Guid messageId, string name) =>
        new() { MessageId = messageId, Name = name };
}
