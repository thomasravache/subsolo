using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Inbox;

/// <summary>
/// SPEC-0007 — job Quartz <c>ProcessInbox</c> do módulo: entrega os eventos de integração recebidos aos
/// <c>IIntegrationEventHandler</c> do módulo, uma única vez por evento.
/// </summary>
internal sealed class ProcessInboxJob<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ModuleDescriptor> modules,
    TimeProvider clock,
    ILogger<ProcessInboxJob<TDbContext>> logger)
    : ProcessMessagesJob<TDbContext, InboxMessage, InboxMessageConsumer>(scopeFactory, modules, clock, logger)
    where TDbContext : ModuleDbContext
{
    protected override string Operation => "ProcessInbox";

    protected override Type? ResolveType(ModuleDescriptor module, string typeName) =>
        module.IntegrationEventTypes.GetValueOrDefault(typeName);

    protected override EventDispatcher Dispatcher(Type eventType) => EventDispatcher.ForIntegrationEvent(eventType);

    protected override InboxMessageConsumer Consumer(Guid messageId, string name) =>
        new() { MessageId = messageId, Name = name };
}
