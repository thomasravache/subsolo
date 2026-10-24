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
/// Seq, ADR-0010) e a próxima execução tenta de novo.
/// </summary>
[DisallowConcurrentExecution]
internal sealed partial class DailyDigestJob(IServiceScopeFactory scopes, ILogger<DailyDigestJob> logger) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Engagement.DailyDigest";

    /// <summary>Chave da configuração com a expressão cron do Quartz.</summary>
    public const string CronSetting = "Engagement:DigestCron";

    /// <summary>Às 9h, todo dia.</summary>
    public const string DefaultCron = "0 0 9 * * ?";

    /// <summary>O fuso do horário do resumo (9h de Brasília).</summary>
    public const string TimeZoneId = "America/Sao_Paulo";

    /// <summary>Agenda o job pela expressão de <see cref="CronSetting"/> (ou <see cref="DefaultCron"/>).</summary>
    public static void Schedule(IQuartzBuilder quartz, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        ArgumentNullException.ThrowIfNull(configuration);
        // Uma expressão inválida faz o Quartz recusar o gatilho na subida (o host não sobe com o resumo quebrado).
        var cron = configuration[CronSetting] is { Length: > 0 } configured ? configured : DefaultCron;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        quartz.ScheduleJob<DailyDigestJob>(
            trigger => trigger
                .WithIdentity(Name)
                .WithCronSchedule(cron, schedule => schedule.InTimeZone(timeZone)),
            job => job.WithIdentity(Name).DisallowConcurrentExecution());
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendDailyDigestCommand>>();
            await handler.Handle(new SendDailyDigestCommand(), cancellationToken);
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
