using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Users.Application.Features.CleanupMagicLinkTokens;

namespace Subsolo.Modules.Users.Infrastructure.MagicLinks;

/// <summary>
/// SPEC-0016 (ADR-0008) — tarefa periódica do Quartz <see cref="Name"/>: roda <see cref="CleanupMagicLinkTokensCommand"/>
/// (apaga os links usados ou expirados há mais de 24 h) a cada <see cref="Interval"/>.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CleanupMagicLinkTokensJob(IServiceScopeFactory scopeFactory) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Users.CleanupMagicLinkTokens";

    /// <summary>Intervalo entre duas limpezas.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CleanupMagicLinkTokensCommand>>();
        await handler.Handle(new CleanupMagicLinkTokensCommand(), cancellationToken);
    }

    /// <summary>
    /// Agenda o job a cada <see cref="Interval"/>, a primeira vez um intervalo depois da subida. Execuções perdidas (host
    /// parado, relógio adiantado) não são recuperadas: a próxima do calendário já faz a limpeza inteira.
    /// </summary>
    public static void Schedule(IQuartzBuilder quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        quartz.ScheduleJob<CleanupMagicLinkTokensJob>(
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
