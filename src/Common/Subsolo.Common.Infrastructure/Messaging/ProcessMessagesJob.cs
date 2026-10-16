using System.Diagnostics;
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
/// tentativa com backoff. Cada handler que já processou a mensagem fica registrado e não roda de novo. Cada
/// mensagem gera uma atividade da fonte <see cref="MessagingTelemetry.SourceName"/>.
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

    /// <summary>Nome do job (<c>ProcessOutbox</c> ou <c>ProcessInbox</c>), prefixo do nome das atividades.</summary>
    protected abstract string Operation { get; }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;

        // Premissa de instância única (ADR-0004): a leitura não usa FOR UPDATE SKIP LOCKED, então duas instâncias
        // do job sobre o mesmo schema processariam as mesmas mensagens. Exigir o bloqueio antes de qualquer
        // sobreposição de instâncias (escala horizontal ou deploy com sobreposição).
        var messages = await db.Set<TMessage>()
            .Where(m => m.ProcessedOnUtc == null && (m.NextAttemptOnUtc == null || m.NextAttemptOnUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            using var activity = MessagingTelemetry.Source.StartActivity($"{Operation} {ShortTypeName(message.Type)}");
            activity?.SetTag(MessagingTelemetry.ModuleTag, _module.Name);
            activity?.SetTag(MessagingTelemetry.MessageIdTag, message.Id);

            try
            {
                await DispatchAsync(db, message, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow().UtcDateTime);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A mensagem da exceção vai para a coluna de erro e para o log: os handlers não devem colocar dados
                // pessoais nas mensagens de exceção (ADR-0008).
                DiscardPendingChanges(db);
                message.MarkFailed(exception, clock.GetUtcNow().UtcDateTime);
                LogFailure(logger, exception, typeof(TMessage).Name, message.Id, _module.Name, message.Attempts);
                activity?.AddException(exception);
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Falha ao gravar o estado desta mensagem não aborta o lote: as alterações pendentes são descartadas
                // e a mensagem volta a ser lida na próxima execução (os handlers já registrados não rodam de novo).
                DiscardPendingChanges(db);
                LogSaveFailure(logger, exception, typeof(TMessage).Name, message.Id, _module.Name);
                activity?.AddException(exception);
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            }
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

    /// <summary>
    /// Desanexa do rastreador as alterações ainda não gravadas (ex.: o registro de consumer cuja gravação falhou),
    /// para que o próximo <c>SaveChanges</c> não as tente de novo.
    /// </summary>
    private static void DiscardPendingChanges(TDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string ShortTypeName(string typeName) => typeName[(typeName.LastIndexOf('.') + 1)..];

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gravar o estado de {MessageKind} {MessageId} do módulo {Module}; nova leitura na próxima execução")]
    private static partial void LogSaveFailure(ILogger logger, Exception exception, string messageKind, Guid messageId, string module);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar {MessageKind} {MessageId} do módulo {Module} (tentativa {Attempt})")]
    private static partial void LogFailure(ILogger logger, Exception exception, string messageKind, Guid messageId, string module, int attempt);
}
