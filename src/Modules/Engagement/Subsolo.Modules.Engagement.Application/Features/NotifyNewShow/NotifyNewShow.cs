using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;

/// <summary>
/// SPEC-0031 — consumidor de <see cref="ShowPublishedIntegrationEvent"/> (pela inbox, idempotente): show ainda
/// <c>scheduled</c> e futuro → registra em <c>published_shows</c> (base do resumo diário) e enfileira o aviso
/// <c>new_show</c> (<see cref="NewShowNotice"/>) para os seguidores únicos da cidade e dos perfis do show
/// (<see cref="IEngagementApi.GetFollowersAsync"/>). O show já começado ou que deixou de estar <c>scheduled</c> até o
/// processamento não avisa ninguém. Entregar o mesmo evento de novo não duplica nada: a linha é por show e o
/// <see cref="INotificationsApi.EnqueueAsync"/> é idempotente por (usuário, chave).
/// </summary>
internal sealed partial class NotifyNewShow(
    IEventsCatalog catalog,
    IEngagementApi engagement,
    IPublishedShows publishedShows,
    INotificationsApi notifications,
    TimeProvider clock,
    ILogger<NotifyNewShow> logger)
    : IIntegrationEventHandler<ShowPublishedIntegrationEvent>
{
    public async Task Handle(ShowPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var now = clock.GetUtcNow();
        var show = await catalog.GetShowSummaryAsync(integrationEvent.ShowId, cancellationToken);
        if (show is null || !NoticeText.IsUpcoming(show, now))
        {
            LogNotNotified(logger, integrationEvent.ShowId, show?.Status ?? "missing");
            return;
        }

        IReadOnlyList<Guid> profileIds = [.. integrationEvent.ProfileIds.Distinct()];
        await publishedShows.AddAsync(new PublishedShow(show.ShowId, show.CitySlug, profileIds, now), cancellationToken);

        var followers = await engagement.GetFollowersAsync(show.CitySlug, profileIds, cancellationToken);
        if (followers.Count == 0)
        {
            LogNoFollowers(logger, show.ShowId);
            return;
        }

        var result = await notifications.EnqueueAsync(followers, NewShowNotice.Compose(show), cancellationToken);
        if (result.IsFailure)
        {
            // Um aviso recusado (inválido) não melhora com nova tentativa da inbox: fica no log.
            LogRejected(logger, show.ShowId, result.Error.Code);
            return;
        }

        EngagementTelemetry.NewShowNotices(followers.Count);
        LogNotified(logger, show.ShowId, followers.Count);
    }

    [LoggerMessage(
        EventName = "NewShowNotified",
        Level = LogLevel.Information,
        Message = "Show novo {ShowId}: aviso enfileirado para {Followers} seguidor(es)")]
    private static partial void LogNotified(ILogger logger, Guid showId, int followers);

    [LoggerMessage(
        EventName = "NewShowWithoutFollowers",
        Level = LogLevel.Information,
        Message = "Show novo {ShowId}: ninguém segue a cidade nem os perfis do show")]
    private static partial void LogNoFollowers(ILogger logger, Guid showId);

    [LoggerMessage(
        EventName = "NewShowNotNotified",
        Level = LogLevel.Information,
        Message = "Show {ShowId} publicado sem aviso: já começou, não existe ou não está agendado ({Status})")]
    private static partial void LogNotNotified(ILogger logger, Guid showId, string status);

    [LoggerMessage(
        EventName = "NewShowNoticeRejected",
        Level = LogLevel.Error,
        Message = "Show novo {ShowId}: o Notifications recusou o aviso ({Code})")]
    private static partial void LogRejected(ILogger logger, Guid showId, string code);
}
