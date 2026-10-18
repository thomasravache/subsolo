using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Application.Features.CleanupProfileRequests;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 (ADR-0008) — tarefa periódica do Quartz <see cref="Name"/>: roda <see cref="CleanupProfileRequestsCommand"/>
/// (apaga os pedidos decididos há mais de 90 dias) a cada <see cref="Interval"/>.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CleanupProfileRequestsJob(IServiceScopeFactory scopeFactory) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Profiles.CleanupProfileRequests";

    /// <summary>Intervalo entre duas limpezas.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CleanupProfileRequestsCommand>>();
        await handler.Handle(new CleanupProfileRequestsCommand(), cancellationToken);
    }

    /// <summary>
    /// Agenda o job a cada <see cref="Interval"/>, a primeira vez um intervalo depois da subida; execuções perdidas não
    /// são recuperadas (a próxima já faz a limpeza inteira).
    /// </summary>
    public static void Schedule(IQuartzBuilder quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        quartz.ScheduleJob<CleanupProfileRequestsJob>(
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
