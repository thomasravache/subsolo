using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Features.Shared;

namespace Subsolo.Modules.Engagement.Application.Features.DailyDigest;

/// <summary>SPEC-0032 (H31-01) — uma execução do gatilho de recuperação do resumo diário (<c>Engagement:DigestCatchUpCron</c>).</summary>
internal sealed record CatchUpDailyDigestCommand : ICommand;

/// <summary>
/// SPEC-0032 (H31-01) — a recuperação do resumo diário (§6, Emenda 2/3): antes das <see cref="DigestHour"/>h de São Paulo
/// não faz nada; depois, se o dia ainda não tem linha em <c>digest_runs</c> (a execução das 9h falhou ou a API estava
/// fora), roda o resumo com a janela (9h de hoje − 24 h, 9h de hoje]. A chave <c>digest:{data}</c> por fã evita e-mail
/// duplicado se uma execução anterior parou no meio.
/// </summary>
internal sealed partial class CatchUpDailyDigestCommandHandler(
    IDigestRuns runs,
    ICommandHandler<SendDailyDigestCommand> digest,
    TimeProvider clock,
    ILogger<CatchUpDailyDigestCommandHandler> logger)
    : ICommandHandler<CatchUpDailyDigestCommand>
{
    /// <summary>
    /// A hora (São Paulo) do resumo regular e do fim da janela da recuperação. SPEC-0036 (H32-02): o
    /// <c>Engagement:DigestCron</c> configurado tem de disparar nela (validado na subida da API).
    /// </summary>
    public const int DigestHour = 9;

    public async Task<Result> Handle(CatchUpDailyDigestCommand command, CancellationToken cancellationToken)
    {
        var local = NoticeText.Local(clock.GetUtcNow(), NoticeText.SaoPaulo);
        if (local.Hour < DigestHour)
        {
            return Result.Success();
        }

        if (await runs.CompletedAsync(DateOnly.FromDateTime(local), cancellationToken))
        {
            return Result.Success();
        }

        var windowEnd = NoticeText.Utc(local.Date.AddHours(DigestHour), NoticeText.SaoPaulo);
        LogCatchingUp(logger, DateOnly.FromDateTime(local));
        return await digest.Handle(new SendDailyDigestCommand(windowEnd), cancellationToken);
    }

    [LoggerMessage(
        EventName = "DailyDigestCatchingUp",
        Level = LogLevel.Warning,
        Message = "Resumo diário de {DigestDate} sem execução concluída: a recuperação roda o resumo agora")]
    private static partial void LogCatchingUp(ILogger logger, DateOnly digestDate);
}
