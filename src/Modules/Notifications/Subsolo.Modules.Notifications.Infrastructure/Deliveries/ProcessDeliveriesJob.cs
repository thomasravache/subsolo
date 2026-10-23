using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Subsolo.Modules.Notifications.Infrastructure.Deliveries;

/// <summary>
/// SPEC-0030 — job Quartz <see cref="Name"/> (a cada <see cref="Interval"/>, sem execuções sobrepostas): envia as
/// entregas pendentes em lotes (<see cref="DeliverySender"/>) até a fila esvaziar ou o limite de lotes por execução.
/// Mesma premissa de instância única da outbox (ADR-0004). Uma falha (banco fora, por exemplo) fica no log e a próxima
/// execução tenta de novo.
/// </summary>
[DisallowConcurrentExecution]
internal sealed partial class ProcessDeliveriesJob(IServiceScopeFactory scopes, ILogger<ProcessDeliveriesJob> logger) : IJob
{
    public const string Name = "Notifications.ProcessDeliveries";

    /// <summary>Intervalo entre execuções.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>Lotes por execução (o resto fica para a próxima).</summary>
    public const int MaxBatchesPerRun = 20;

    public static void Schedule(IQuartzBuilder quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        quartz.ScheduleJob<ProcessDeliveriesJob>(
            trigger => trigger
                .WithIdentity(Name)
                .StartNow()
                .WithSimpleSchedule(schedule => schedule.WithInterval(Interval).RepeatForever()),
            job => job.WithIdentity(Name).DisallowConcurrentExecution());
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                await using var scope = scopes.CreateAsyncScope();
                var processed = await scope.ServiceProvider.GetRequiredService<DeliverySender>().ProcessBatchAsync(cancellationToken);
                if (processed < DeliverySender.BatchSize)
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(logger, exception);
        }
    }

    [LoggerMessage(EventName = "NoticeDeliveriesRunFailed", Level = LogLevel.Error, Message = "Falha ao processar as entregas de avisos; nova tentativa na próxima execução")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
