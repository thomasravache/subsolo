using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Application.Media;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Application.Features.ListMyShows;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>SPEC-0023 — leituras de <c>events.shows</c> sem rastreamento (agenda, "Meus shows" e a tela de edição).</summary>
internal sealed class ShowsReadModel(EventsDbContext db) : IShowsReadModel
{
    public async Task<IReadOnlyList<ShowSummaryResponse>> ListAgendaAsync(
        string city, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var shows = await db.Shows.AsNoTracking()
            .Where(show => show.City == city && show.StartsAtUtc >= fromUtc && show.StartsAtUtc < toUtc)
            .OrderBy(show => show.StartsAtUtc)
            .ThenBy(show => show.Id)
            .ToListAsync(cancellationToken);
        return
        [
            .. shows.Select(show => new ShowSummaryResponse(
                show.Id,
                show.Slug,
                show.Title,
                show.StartsAtUtc.UtcDateTime,
                show.Venue.Name,
                [.. show.Lineup.Select(item => item.Name)],
                show.Styles,
                ShowStatusMapping.ToResponse(show.Status),
                ThumbUrl(show.FlyerId),
                AttendingCount: 0)),
        ];
    }

    public async Task<IReadOnlyList<MyShowResponse>> ListMineAsync(
        IReadOnlyCollection<Guid> organizerProfileIds,
        Guid? createdBy,
        DateTimeOffset boundaryUtc,
        bool upcoming,
        CancellationToken cancellationToken)
    {
        var mine = db.Shows.AsNoTracking()
            .Where(show => createdBy != null ? show.CreatedBy == createdBy : organizerProfileIds.Contains(show.OrganizerProfileId));
        mine = upcoming
            ? mine.Where(show => show.StartsAtUtc >= boundaryUtc).OrderBy(show => show.StartsAtUtc)
            : mine.Where(show => show.StartsAtUtc < boundaryUtc).OrderByDescending(show => show.StartsAtUtc);
        var shows = await mine.ToListAsync(cancellationToken);
        return
        [
            .. shows.Select(show => new MyShowResponse(
                show.Id,
                show.Slug,
                show.Title,
                show.StartsAtUtc.UtcDateTime,
                show.City,
                show.Venue.Name,
                ShowStatusMapping.ToResponse(show.Status),
                ThumbUrl(show.FlyerId),
                show.OrganizerProfileId)),
        ];
    }

    public async Task<ShowDetails?> GetAsync(Guid showId, CancellationToken cancellationToken)
    {
        var show = await db.Shows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        return show is null ? null : Details(show);
    }

    private static ShowDetails Details(Show show) => new(
        show.Id,
        show.Slug,
        show.OrganizerProfileId,
        show.Title,
        show.City,
        show.StartsAtUtc,
        new ShowPartResponse(show.Venue.ProfileId, show.Venue.Name),
        [.. show.Lineup.Select(item => new ShowPartResponse(item.ProfileId, item.Name))],
        show.Styles,
        show.Price,
        show.TicketUrl,
        show.FlyerId,
        show.Status);

    private static string ThumbUrl(string flyerId) => MediaPaths.PublicUrl(Flyer.VariantPath(flyerId, Flyer.Thumb));
}
