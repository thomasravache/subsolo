using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.CleanupFlyers;

namespace Subsolo.Modules.Events.Infrastructure.Flyers;

/// <summary>
/// SPEC-0022 — tarefa periódica do Quartz <see cref="Name"/>: roda <see cref="CleanupFlyersCommand"/> (apaga os flyers
/// sem show criados há mais de 24 h) a cada <see cref="Interval"/> (copiado do <c>CleanupProfileRequestsJob</c> da SPEC-0021).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CleanupFlyersJob(IServiceScopeFactory scopeFactory) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Events.CleanupFlyers";

    /// <summary>Intervalo entre duas limpezas.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CleanupFlyersCommand>>();
        await handler.Handle(new CleanupFlyersCommand(), cancellationToken);
    }

    /// <summary>
    /// Agenda o job a cada <see cref="Interval"/>, a primeira vez um intervalo depois da subida; execuções perdidas não
    /// são recuperadas (a próxima já faz a limpeza inteira).
    /// </summary>
    public static void Schedule(IQuartzBuilder quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        quartz.ScheduleJob<CleanupFlyersJob>(
            trigger => trigger
                .WithIdentity(Name)
                .StartAt(DateTimeOffset.UtcNow + Interval)
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(Interval)
                    .RepeatForever()
                    .WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount)),
            job => job.WithIdentity(Name));
    }
}
