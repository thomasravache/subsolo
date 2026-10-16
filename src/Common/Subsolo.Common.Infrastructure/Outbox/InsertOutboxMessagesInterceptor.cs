using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Subsolo.Common.Domain;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Outbox;

/// <summary>
/// SPEC-0007 — antes do <c>SaveChanges</c> de um <see cref="ModuleDbContext"/>, transforma os eventos de domínio
/// pendentes das entidades em linhas da outbox do módulo, que são gravadas no mesmo comando (mesma transação).
/// </summary>
internal sealed class InsertOutboxMessagesInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        InsertOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        InsertOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void InsertOutboxMessages(DbContext? context)
    {
        if (context is not ModuleDbContext)
        {
            return;
        }

        var occurredOnUtc = clock.GetUtcNow().UtcDateTime;
        var messages = context.ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var domainEvents = entity.DomainEvents;
                entity.ClearDomainEvents();
                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage
            {
                // O Id da mensagem é o do evento de domínio: identidade estável entre tentativas.
                Id = domainEvent.Id,
                Type = MessageSerializer.TypeName(domainEvent.GetType()),
                Content = MessageSerializer.Serialize(domainEvent),
                OccurredOnUtc = occurredOnUtc,
            })
            .ToList();

        context.Set<OutboxMessage>().AddRange(messages);
    }
}
