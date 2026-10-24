using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;

/// <summary>
/// SPEC-0031 — o compositor do aviso <c>new_show</c> (§6): título "Show novo em {CityName}", corpo
/// "{Lineup separado por ' + '} · {ddd dd/MM HH'h'mm no fuso da cidade} · {VenueName}", url "/{CitySlug}/{Slug}" (caminho
/// do site) e chave "new_show:{ShowId}". Só dados públicos do show (ADR-0008).
/// </summary>
internal static class NewShowNotice
{
    public const string Kind = "new_show";

    public static Notice Compose(ShowSummary show)
    {
        ArgumentNullException.ThrowIfNull(show);
        var local = NoticeText.Local(show.StartsAtUtc, show.CityTimeZone);
        return new Notice(
            Kind,
            $"new_show:{show.ShowId:D}",
            $"Show novo em {show.CityName}",
            $"{NoticeText.Lineup(show.Lineup)} · {NoticeText.DayAndTime(local)} · {show.VenueName}",
            $"/{show.CitySlug}/{show.Slug}");
    }
}
