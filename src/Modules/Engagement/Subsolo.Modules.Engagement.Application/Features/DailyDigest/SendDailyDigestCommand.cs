using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DailyDigest;

/// <summary>SPEC-0031 — uma execução do resumo diário (job <c>Engagement.DailyDigest</c>).</summary>
internal sealed record SendDailyDigestCommand : ICommand;

/// <summary>
/// SPEC-0031 — apaga de <c>published_shows</c> as linhas com mais de <see cref="Retention"/>, monta o resumo dos shows
/// publicados nas últimas <see cref="Window"/> (<see cref="DigestBuilder"/>) e enfileira os avisos <c>digest</c> no
/// Notifications, uma chamada por corpo igual (os fãs com os mesmos shows vão juntos). O Notifications só manda o e-mail a
/// quem ligou o resumo; a chave <c>digest:{data}</c> faz uma segunda execução no mesmo dia não mandar outro, e uma
/// execução que falhou no meio retoma na próxima.
/// </summary>
internal sealed partial class SendDailyDigestCommandHandler(
    IPublishedShows publishedShows,
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
        var now = clock.GetUtcNow();
        var deleted = await publishedShows.DeletePublishedBeforeAsync(now - Retention, cancellationToken);
        var published = await publishedShows.PublishedSinceAsync(now - Window, cancellationToken);
        var notices = await builder.BuildAsync(published, cancellationToken);

        foreach (var group in notices.GroupBy(notice => notice.Notice))
        {
            var result = await notifications.EnqueueAsync([.. group.Select(notice => notice.UserId)], group.Key, cancellationToken);
            if (result.IsFailure)
            {
                LogRejected(logger, result.Error.Code);
            }
        }

        EngagementTelemetry.DigestRecipients(notices.Count);
        LogCompleted(logger, published.Count, notices.Count, deleted);
        return Result.Success();
    }

    [LoggerMessage(
        EventName = "DailyDigestCompleted",
        Level = LogLevel.Information,
        Message = "Resumo diário: {Shows} show(s) publicado(s) nas últimas 24 h, {Recipients} fã(s) com resumo, {Deleted} linha(s) antiga(s) apagada(s)")]
    private static partial void LogCompleted(ILogger logger, int shows, int recipients, int deleted);

    [LoggerMessage(
        EventName = "DailyDigestNoticeRejected",
        Level = LogLevel.Error,
        Message = "Resumo diário: o Notifications recusou um aviso ({Code})")]
    private static partial void LogRejected(ILogger logger, string code);
}
