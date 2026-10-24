using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>
/// SPEC-0031 — job Quartz <see cref="Name"/>: roda <see cref="SendDailyDigestCommand"/> no horário de
/// <c>Engagement:DigestCron</c> (padrão <see cref="DefaultCron"/>, às 9h) no fuso <see cref="TimeZoneId"/>, sem execuções
/// sobrepostas. Uma falha (banco fora, por exemplo) vira o evento <c>DailyDigestRunFailed</c> (alerta "Falha de job" do
/// Seq, ADR-0010) e a próxima execução tenta de novo. SPEC-0032 (H31-01): no gatilho regular a janela termina no horário
/// agendado da execução (um disparo manual usa agora); um segundo gatilho, <see cref="CatchUpName"/>
/// (<c>Engagement:DigestCatchUpCron</c>, padrão <see cref="DefaultCatchUpCron"/>), roda a recuperação
/// (<see cref="CatchUpDailyDigestCommand"/>) no mesmo job, então nunca em paralelo com o regular.
/// </summary>
[DisallowConcurrentExecution]
internal sealed partial class DailyDigestJob(IServiceScopeFactory scopes, ILogger<DailyDigestJob> logger) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Engagement.DailyDigest";

    /// <summary>
    /// Chave da configuração com a expressão cron do Quartz. SPEC-0036 (H32-02): a expressão tem de disparar às 9h de São
    /// Paulo (<see cref="Application.Features.DailyDigest.CatchUpDailyDigestCommandHandler.DigestHour"/>, a hora em que a
    /// recuperação fecha a janela); outra hora impede a subida (<see cref="DigestScheduleOptionsValidator"/>). Expressões
    /// mais frequentes que também disparam às 9h (a cada minuto, na pilha local) valem.
    /// </summary>
    public const string CronSetting = "Engagement:DigestCron";

    /// <summary>Às 9h (<see cref="Application.Features.DailyDigest.CatchUpDailyDigestCommandHandler.DigestHour"/>), todo dia.</summary>
    public const string DefaultCron = "0 0 9 * * ?";

    /// <summary>SPEC-0032 (H31-01) — identidade do gatilho de recuperação no Quartz.</summary>
    public const string CatchUpName = "Engagement.DailyDigest.CatchUp";

    /// <summary>SPEC-0032 (H31-01) — chave da configuração com a expressão cron da recuperação.</summary>
    public const string CatchUpCronSetting = "Engagement:DigestCatchUpCron";

    /// <summary>SPEC-0032 (H31-01) — a cada 15 minutos.</summary>
    public const string DefaultCatchUpCron = "0 0/15 * * * ?";

    /// <summary>O fuso do horário do resumo (9h de Brasília).</summary>
    public const string TimeZoneId = "America/Sao_Paulo";

    /// <summary>
    /// Agenda o job pela expressão de <see cref="CronSetting"/> (ou <see cref="DefaultCron"/>) e, SPEC-0032, o gatilho de
    /// recuperação pela de <see cref="CatchUpCronSetting"/> (ou <see cref="DefaultCatchUpCron"/>).
    /// </summary>
    public static void Schedule(IQuartzBuilder quartz, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        ArgumentNullException.ThrowIfNull(configuration);
        // Uma expressão inválida faz o Quartz recusar o gatilho na subida (o host não sobe com o resumo quebrado).
        var cron = CronOf(configuration);
        var catchUpCron = configuration[CatchUpCronSetting] is { Length: > 0 } catchUp ? catchUp : DefaultCatchUpCron;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        quartz.ScheduleJob<DailyDigestJob>(
            trigger => trigger
                .WithIdentity(Name)
                .WithCronSchedule(cron, schedule => schedule.InTimeZone(timeZone)),
            job => job.WithIdentity(Name).DisallowConcurrentExecution());
        quartz.AddTrigger(trigger => trigger
            .ForJob(Name)
            .WithIdentity(CatchUpName)
            .WithCronSchedule(catchUpCron, schedule => schedule.InTimeZone(timeZone)));
    }

    /// <summary>A expressão de <see cref="CronSetting"/>, ou <see cref="DefaultCron"/> sem ela.</summary>
    public static string CronOf(IConfiguration configuration) =>
        configuration[CronSetting] is { Length: > 0 } configured ? configured : DefaultCron;

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            if (context.Trigger.Key.Name == CatchUpName)
            {
                var catchUp = scope.ServiceProvider.GetRequiredService<ICommandHandler<CatchUpDailyDigestCommand>>();
                await catchUp.Handle(new CatchUpDailyDigestCommand(), cancellationToken);
                return;
            }

            // O gatilho regular fecha a janela no horário agendado (um atraso na execução não desloca a janela); um
            // disparo manual (TriggerJob) a fecha agora, como antes.
            var windowEnd = context.Trigger.Key.Name == Name ? context.ScheduledFireTimeUtc : null;
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendDailyDigestCommand>>();
            await handler.Handle(new SendDailyDigestCommand(windowEnd), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "DailyDigestRunFailed",
        Level = LogLevel.Error,
        Message = "Falha ao montar o resumo diário; nova tentativa na próxima execução")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
