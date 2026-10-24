using Microsoft.Extensions.Options;
using Quartz;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>
/// SPEC-0036 (H32-02) — a expressão cron do resumo diário (<see cref="DailyDigestJob.CronSetting"/>, ou
/// <see cref="DailyDigestJob.DefaultCron"/>), validada na subida por <see cref="DigestScheduleOptionsValidator"/>.
/// </summary>
internal sealed class DigestScheduleOptions
{
    public string Cron { get; set; } = DailyDigestJob.DefaultCron;
}

/// <summary>
/// SPEC-0036 (H32-02) — a recuperação do resumo fecha a janela às <see cref="CatchUpDailyDigestCommandHandler.DigestHour"/>h
/// de São Paulo (<see cref="CatchUpDailyDigestCommandHandler"/>), então <c>Engagement:DigestCron</c> tem de disparar
/// nessa hora: a API não sobe se a expressão não dispara às 09h00 de São Paulo do dia corrente pelo relógio da aplicação
/// (<see cref="CronExpression.IsSatisfiedBy"/>). "0 0 9 * * ?" (o padrão) e "0 * * * * ?" (a cada minuto, da pilha local)
/// passam; "0 0 10 * * ?" não (a recuperação rodaria antes do resumo, todo dia).
/// </summary>
internal sealed class DigestScheduleOptionsValidator(TimeProvider clock) : IValidateOptions<DigestScheduleOptions>
{
    public ValidateOptionsResult Validate(string? name, DigestScheduleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hour = CatchUpDailyDigestCommandHandler.DigestHour;
        return FiresAtDigestHour(options.Cron, clock.GetUtcNow())
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{DailyDigestJob.CronSetting} (\"{options.Cron}\") deve disparar às {hour:00}h00 de São Paulo ({hour}h, a hora do resumo e do fim da janela da recuperação).");
    }

    /// <summary>A expressão é válida e dispara às <c>DigestHour</c>h00 de São Paulo do dia de <paramref name="nowUtc"/> lá.</summary>
    private static bool FiresAtDigestHour(string cron, DateTimeOffset nowUtc)
    {
        if (!CronExpression.TryParse(cron, out var expression))
        {
            return false;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(DailyDigestJob.TimeZoneId);
        var digestLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date.AddHours(CatchUpDailyDigestCommandHandler.DigestHour);
        var digest = new DateTimeOffset(digestLocal, timeZone.GetUtcOffset(digestLocal));
        return expression.WithTimeZone(timeZone).IsSatisfiedBy(digest);
    }
}
