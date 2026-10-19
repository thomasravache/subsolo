using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Application.Media;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Application.Features.GetShowDetail;
using Subsolo.Modules.Events.Application.Features.ListMyShows;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>
/// SPEC-0023 — leituras de <c>events.shows</c> sem rastreamento (agenda, "Meus shows" e a tela de edição). SPEC-0024: a
/// página pública do show (com as medidas do flyer) e o fuso da cidade em "Meus shows".
/// </summary>
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
            .Where(show => createdBy != null ? show.CreatedBy == createdBy : organizerProfileIds.Contains(show.OrganizerProfileId))
            .Where(show => upcoming ? show.StartsAtUtc >= boundaryUtc : show.StartsAtUtc < boundaryUtc)
            .Join(db.Cities.AsNoTracking(), show => show.City, city => city.Slug, (show, city) => new { Show = show, city.TimeZone });
        mine = upcoming
            ? mine.OrderBy(item => item.Show.StartsAtUtc)
            : mine.OrderByDescending(item => item.Show.StartsAtUtc);
        var shows = await mine.ToListAsync(cancellationToken);
        return
        [
            .. shows.Select(item => new MyShowResponse(
                item.Show.Id,
                item.Show.Slug,
                item.Show.Title,
                item.Show.StartsAtUtc.UtcDateTime,
                item.Show.City,
                item.TimeZone,
                item.Show.Venue.Name,
                ShowStatusMapping.ToResponse(item.Show.Status),
                ThumbUrl(item.Show.FlyerId),
                item.Show.OrganizerProfileId)),
        ];
    }

    public async Task<ShowDetails?> GetAsync(Guid showId, CancellationToken cancellationToken)
    {
        var show = await db.Shows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        return show is null ? null : Details(show);
    }

    public async Task<PublicShowDetails?> GetBySlugAsync(string city, string slug, CancellationToken cancellationToken)
    {
        var found = await db.Shows.AsNoTracking()
            .Where(show => show.City == city && show.Slug == slug)
            .Join(
                db.Flyers.AsNoTracking(),
                show => show.FlyerId,
                flyer => flyer.Id,
                (show, flyer) => new { Show = show, flyer.FullWidth, flyer.FullHeight })
            .SingleOrDefaultAsync(cancellationToken);
        return found is null ? null : new PublicShowDetails(Details(found.Show), found.FullWidth, found.FullHeight);
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
