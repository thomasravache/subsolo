using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DailyDigest;

/// <summary>
/// SPEC-0031 — uma execução do resumo diário (job <c>Engagement.DailyDigest</c>). SPEC-0032 (H31-01): a janela termina em
/// <paramref name="WindowEndUtc"/> (o horário agendado do gatilho, ou as 9h de hoje na recuperação); sem ele, agora.
/// </summary>
internal sealed record SendDailyDigestCommand(DateTimeOffset? WindowEndUtc = null) : ICommand;

/// <summary>
/// SPEC-0031 — apaga de <c>published_shows</c> as linhas com mais de <see cref="Retention"/>, monta o resumo dos shows
/// publicados na janela (fim − <see cref="Window"/>, fim] (<see cref="DigestBuilder"/>) e enfileira os avisos <c>digest</c> no
/// Notifications, uma chamada por corpo igual (os fãs com os mesmos shows vão juntos). O Notifications só manda o e-mail a
/// quem ligou o resumo; a chave <c>digest:{data}</c> faz uma segunda execução no mesmo dia não mandar outro, e uma
/// execução que falhou no meio retoma na próxima. SPEC-0032 (H31-01): ao concluir, grava o dia (São Paulo, do fim da
/// janela) em <c>digest_runs</c>, que o gatilho de recuperação consulta.
/// </summary>
internal sealed partial class SendDailyDigestCommandHandler(
    IPublishedShows publishedShows,
    IDigestRuns runs,
    DigestBuilder builder,
    INotificationsApi notifications,
    TimeProvider clock,
    ILogger<SendDailyDigestCommandHandler> logger)
    : ICommandHandler<SendDailyDigestCommand>
{
    /// <summary>Os shows publicados neste intervalo entram no resumo.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>Por quanto tempo uma linha de <c>published_shows</c> fica guardada.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<Result> Handle(SendDailyDigestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var end = command.WindowEndUtc ?? now;
        var start = end - Window;
        var deleted = await publishedShows.DeletePublishedBeforeAsync(now - Retention, cancellationToken);
        IReadOnlyList<PublishedShow> published =
        [
            .. (await publishedShows.PublishedSinceAsync(start, cancellationToken))
                .Where(row => row.PublishedAtUtc > start && row.PublishedAtUtc <= end),
        ];
        var notices = await builder.BuildAsync(published, cancellationToken);

        foreach (var group in notices.GroupBy(notice => notice.Notice))
        {
            var result = await notifications.EnqueueAsync([.. group.Select(notice => notice.UserId)], group.Key, cancellationToken);
            if (result.IsFailure)
            {
                LogRejected(logger, result.Error.Code);
            }
        }

        await runs.RecordAsync(DateOnly.FromDateTime(NoticeText.Local(end, NoticeText.SaoPaulo)), clock.GetUtcNow(), cancellationToken);
        EngagementTelemetry.DigestRecipients(notices.Count);
        LogCompleted(logger, published.Count, notices.Count, deleted);
        return Result.Success();
    }

    [LoggerMessage(
        EventName = "DailyDigestCompleted",
        Level = LogLevel.Information,
        Message = "Resumo diário: {Shows} show(s) publicado(s) nas 24 h da janela, {Recipients} fã(s) considerado(s), {Deleted} linha(s) antiga(s) apagada(s)")]
    private static partial void LogCompleted(ILogger logger, int shows, int recipients, int deleted);

    [LoggerMessage(
        EventName = "DailyDigestNoticeRejected",
        Level = LogLevel.Error,
        Message = "Resumo diário: o Notifications recusou um aviso ({Code})")]
    private static partial void LogRejected(ILogger logger, string code);
}
