using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Engagement.Application.Features.DayBeforeReminder;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>
/// SPEC-0032 — job Quartz <see cref="Name"/>: roda <see cref="SendDayBeforeRemindersCommand"/> no horário de
/// <c>Engagement:ReminderCron</c> (padrão <see cref="DefaultCron"/>, de hora em hora) com a hora local de
/// <c>Engagement:ReminderHour</c> (padrão <see cref="DefaultReminderHour"/>), sem execuções sobrepostas. Uma falha (banco
/// fora, por exemplo) vira o evento <c>DayBeforeReminderRunFailed</c> (alerta "Falha de job" do Seq, ADR-0010) e a próxima
/// hora tenta de novo (a chave do aviso evita lembrete duplicado).
/// </summary>
[DisallowConcurrentExecution]
internal sealed partial class DayBeforeReminderJob(IServiceScopeFactory scopes, ILogger<DayBeforeReminderJob> logger) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Engagement.DayBeforeReminder";

    /// <summary>Chave da configuração com a expressão cron do Quartz.</summary>
    public const string CronSetting = "Engagement:ReminderCron";

    /// <summary>Chave da configuração com a hora local (0–23) a partir da qual a véspera vale.</summary>
    public const string HourSetting = "Engagement:ReminderHour";

    /// <summary>De hora em hora, no minuto zero.</summary>
    public const string DefaultCron = "0 0 * * * ?";

    /// <summary>10h no fuso da cidade (ADR-0014).</summary>
    public const int DefaultReminderHour = 10;

    /// <summary>Chave da hora do lembrete nos dados do job.</summary>
    private const string HourKey = "reminderHour";

    /// <summary>Agenda o job pela expressão de <see cref="CronSetting"/> (ou <see cref="DefaultCron"/>).</summary>
    public static void Schedule(IQuartzBuilder quartz, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        ArgumentNullException.ThrowIfNull(configuration);
        // Uma expressão ou hora inválida faz a subida falhar (o host não sobe com o lembrete quebrado).
        var cron = configuration[CronSetting] is { Length: > 0 } configured ? configured : DefaultCron;
        var hour = configuration.GetValue(HourSetting, DefaultReminderHour);
        if (hour is < 0 or > 23)
        {
            throw new InvalidOperationException($"{HourSetting} deve estar entre 0 e 23 (veio {hour}).");
        }

        quartz.ScheduleJob<DayBeforeReminderJob>(
            trigger => trigger
                .WithIdentity(Name)
                .WithCronSchedule(cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc)),
            job => job.WithIdentity(Name).DisallowConcurrentExecution().UsingJobData(HourKey, hour));
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var hour = context.MergedJobDataMap.TryGetInt(HourKey, out var configured) ? configured : DefaultReminderHour;
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendDayBeforeRemindersCommand>>();
            await handler.Handle(new SendDayBeforeRemindersCommand(hour), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(logger, exception);
        }
    }

    [LoggerMessage(
        EventName = "DayBeforeReminderRunFailed",
        Level = LogLevel.Error,
        Message = "Falha ao enfileirar os lembretes na véspera; nova tentativa na próxima execução")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
