using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Infrastructure.Inbox;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.EventBus;

/// <summary>
/// SPEC-0007 — barramento em processo: grava o evento de integração na inbox de cada módulo que tem handler para
/// ele. A inbox usa o id do evento como chave, então uma entrega repetida é ignorada (idempotência).
/// </summary>
internal sealed class InProcessEventBus(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ModuleDescriptor> modules)
    : IEventBus
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules = [.. modules];

    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var type = integrationEvent.GetType();
        var typeName = MessageSerializer.TypeName(type);
        var content = MessageSerializer.Serialize(integrationEvent);

        foreach (var module in _modules.Where(m => m.IntegrationEventTypes.TryGetValue(typeName, out var known) && known == type))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType);
            if (await db.Set<InboxMessage>().AnyAsync(m => m.Id == integrationEvent.Id, cancellationToken))
            {
                continue;
            }

            db.Set<InboxMessage>().Add(new InboxMessage
            {
                Id = integrationEvent.Id,
                Type = typeName,
                Content = content,
                OccurredOnUtc = integrationEvent.OccurredOnUtc.Kind == DateTimeKind.Local
                    ? integrationEvent.OccurredOnUtc.ToUniversalTime()
                    : DateTime.SpecifyKind(integrationEvent.OccurredOnUtc, DateTimeKind.Utc),
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Entrega concorrente do mesmo evento: já está na inbox.
            }
        }
    }
}
