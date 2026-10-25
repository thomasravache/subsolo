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
/// nessa hora. SPEC-0038 (H36-03): a API não sobe se a expressão não dispara às 09h00 de São Paulo em nenhum dos próximos
/// <see cref="WindowDays"/> dias, a partir do dia corrente pelo relógio da aplicação (<see cref="CronExpression.IsSatisfiedBy"/>).
/// "0 0 9 * * ?" (o padrão), "0 0 9 ? * MON-FRI" (dias úteis) e "0 * * * * ?" (a cada minuto, da pilha local) passam;
/// "0 0 10 * * ?" não (a recuperação rodaria antes do resumo, todo dia). SPEC-0038 (H36-04): expressão que não passa no
/// <see cref="CronExpression.TryParse"/> falha com mensagem própria.
/// </summary>
internal sealed class DigestScheduleOptionsValidator(TimeProvider clock) : IValidateOptions<DigestScheduleOptions>
{
    /// <summary>Quantos dias, a partir do corrente, a expressão pode levar para disparar às 9h (uma semana).</summary>
    internal const int WindowDays = 7;

    public ValidateOptionsResult Validate(string? name, DigestScheduleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!CronExpression.TryParse(options.Cron, out var expression))
        {
            return ValidateOptionsResult.Fail(
                $"{DailyDigestJob.CronSetting} (\"{options.Cron}\") não é uma expressão cron válida.");
        }

        var hour = CatchUpDailyDigestCommandHandler.DigestHour;
        return FiresAtDigestHourWithinAWeek(expression, clock.GetUtcNow())
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{DailyDigestJob.CronSetting} (\"{options.Cron}\") deve disparar às {hour:00}h00 de São Paulo em pelo menos um dia da semana ({hour}h, a hora do resumo e do fim da janela da recuperação).");
    }

    /// <summary>
    /// A expressão dispara às <c>DigestHour</c>h00 de São Paulo em algum dos <see cref="WindowDays"/> dias a partir do dia
    /// de <paramref name="nowUtc"/> lá.
    /// </summary>
    private static bool FiresAtDigestHourWithinAWeek(CronExpression expression, DateTimeOffset nowUtc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(DailyDigestJob.TimeZoneId);
        var zoned = expression.WithTimeZone(timeZone);
        var today = TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date;
        for (var day = 0; day < WindowDays; day++)
        {
            var digestLocal = today.AddDays(day).AddHours(CatchUpDailyDigestCommandHandler.DigestHour);
            var digest = new DateTimeOffset(digestLocal, timeZone.GetUtcOffset(digestLocal));
            if (zoned.IsSatisfiedBy(digest))
            {
                return true;
            }
        }

        return false;
    }
}
