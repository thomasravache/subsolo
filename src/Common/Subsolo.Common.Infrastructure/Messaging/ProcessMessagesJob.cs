using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Subsolo.Common.Infrastructure.Database;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>
/// SPEC-0007 — base dos jobs Quartz <c>ProcessOutbox</c> e <c>ProcessInbox</c> de um módulo: lê as mensagens
/// pendentes do schema do módulo (cujo próximo horário de tentativa já chegou, pelo <see cref="TimeProvider"/>),
/// entrega cada uma aos handlers do módulo e marca como processada; numa falha, registra o erro e agenda nova
/// tentativa com backoff. Cada handler que já processou a mensagem fica registrado e não roda de novo.
/// </summary>
internal abstract partial class ProcessMessagesJob<TDbContext, TMessage, TConsumer>(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ModuleDescriptor> modules,
    TimeProvider clock,
    ILogger logger)
    : IJob
    where TDbContext : ModuleDbContext
    where TMessage : ModuleMessage
    where TConsumer : ModuleMessageConsumer
{
    /// <summary>Mensagens lidas por execução do job.</summary>
    internal const int BatchSize = 20;

    private readonly ModuleDescriptor _module = modules.Single(module => module.DbContextType == typeof(TDbContext));

    /// <summary>Tipo do evento guardado com o nome <paramref name="typeName"/>, ou <see langword="null"/>.</summary>
    protected abstract Type? ResolveType(ModuleDescriptor module, string typeName);

    protected abstract EventDispatcher Dispatcher(Type eventType);

    protected abstract TConsumer Consumer(Guid messageId, string name);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;

        var messages = await db.Set<TMessage>()
            .Where(m => m.ProcessedOnUtc == null && (m.NextAttemptOnUtc == null || m.NextAttemptOnUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(db, message, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow().UtcDateTime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception, clock.GetUtcNow().UtcDateTime);
                LogFailure(logger, exception, typeof(TMessage).Name, message.Id, _module.Name, message.Attempts);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchAsync(TDbContext db, TMessage message, CancellationToken cancellationToken)
    {
        var type = ResolveType(_module, message.Type)
                   ?? throw new InvalidOperationException($"Tipo de evento desconhecido no módulo {_module.Name}: {message.Type}.");
        var payload = MessageSerializer.Deserialize(message.Content, type);
        var dispatcher = Dispatcher(type);

        await using var handlerScope = scopeFactory.CreateAsyncScope();
        foreach (var handler in dispatcher.Handlers(handlerScope.ServiceProvider).Where(_module.Owns))
        {
            var name = MessageSerializer.TypeName(handler.GetType());
            var done = await db.Set<TConsumer>()
                .AnyAsync(c => c.MessageId == message.Id && c.Name == name, cancellationToken);
            if (done)
            {
                continue;
            }

            await dispatcher.HandleAsync(handler, payload, cancellationToken);
            db.Set<TConsumer>().Add(Consumer(message.Id, name));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar {MessageKind} {MessageId} do módulo {Module} (tentativa {Attempt})")]
    private static partial void LogFailure(ILogger logger, Exception exception, string messageKind, Guid messageId, string module, int attempt);
}
