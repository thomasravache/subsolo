using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Application.Media;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Application.Features.GetShowDetail;
using Subsolo.Modules.Events.Application.Features.ListMyShows;
using Subsolo.Modules.Events.Application.Features.ListProfileShows;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>
/// SPEC-0023 — leituras de <c>events.shows</c> sem rastreamento (agenda, "Meus shows" e a tela de edição). SPEC-0024: a
/// página pública do show (com as medidas do flyer) e o fuso da cidade em "Meus shows". SPEC-0026: os shows de um
/// perfil. SPEC-0033: o show oculto pela moderação fica fora de toda leitura pública (agenda, página e shows do perfil);
/// só "Meus shows" e a tela de edição do dono o devolvem, com <c>hidden</c>.
/// </summary>
internal sealed class ShowsReadModel(EventsDbContext db) : IShowsReadModel
{
    /// <summary>O mesmo formato do line-up gravado (<c>ShowConfiguration</c>: <c>[{"profileId": …, "name": …}]</c>).</summary>
    private static readonly JsonSerializerOptions LineupJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ShowSummaryResponse>> ListAgendaAsync(
        string city,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<string> styles,
        CancellationToken cancellationToken)
    {
        var query = db.Shows.AsNoTracking()
            .Where(show => show.HiddenAtUtc == null)
            .Where(show => show.City == city && show.StartsAtUtc >= fromUtc && show.StartsAtUtc < toUtc);
        if (styles.Count > 0)
        {
            // SPEC-0025: só os estilos que existem filtram; com nenhum deles, a agenda segue sem filtro de estilo.
            var requested = styles.ToList();
            var known = await db.Styles.AsNoTracking()
                .Where(style => requested.Contains(style.Slug))
                .Select(style => style.Slug)
                .ToListAsync(cancellationToken);
            if (known.Count > 0)
            {
                query = query.Where(show => show.Styles.Any(style => known.Contains(style)));
            }
        }

        var shows = await query
            .OrderBy(show => show.StartsAtUtc)
            .ThenBy(show => show.Id)
            .ToListAsync(cancellationToken);
        return [.. shows.Select(Summary)];
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
                item.Show.OrganizerProfileId,
                item.Show.HiddenAtUtc != null)),
        ];
    }

    /// <summary>
    /// SPEC-0026 — o perfil como organizador, casa (<c>venue_profile_id</c>) ou banda do line-up (<c>lineup @&gt;
    /// [{"profileId": …}]</c>, com o índice GIN da migração <c>ShowProfileIndexes</c>), com a cidade de cada show.
    /// </summary>
    public async Task<IReadOnlyList<ProfileShowResponse>> ListByProfileAsync(
        Guid profileId,
        DateTimeOffset boundaryUtc,
        bool upcoming,
        int limit,
        CancellationToken cancellationToken)
    {
        var lineup = JsonSerializer.Serialize(new[] { new { profileId } }, LineupJson);
        // Os ids na ordem e no limite pedidos, num SQL só (o filtro do line-up em jsonb não tem tradução no LINQ).
        var ids = await (upcoming
                ? db.Database.SqlQuery<Guid>(
                    $"""
                    SELECT id AS "Value" FROM events.shows
                    WHERE (organizer_profile_id = {profileId} OR venue_profile_id = {profileId} OR lineup @> CAST({lineup} AS jsonb))
                      AND hidden_at_utc IS NULL
                      AND starts_at_utc >= {boundaryUtc}
                    ORDER BY starts_at_utc, id
                    LIMIT {limit}
                    """)
                : db.Database.SqlQuery<Guid>(
                    $"""
                    SELECT id AS "Value" FROM events.shows
                    WHERE (organizer_profile_id = {profileId} OR venue_profile_id = {profileId} OR lineup @> CAST({lineup} AS jsonb))
                      AND hidden_at_utc IS NULL
                      AND starts_at_utc < {boundaryUtc}
                    ORDER BY starts_at_utc DESC, id DESC
                    LIMIT {limit}
                    """))
            .ToListAsync(cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        var shows = await db.Shows.AsNoTracking()
            .Where(show => ids.Contains(show.Id))
            .Join(db.Cities.AsNoTracking(), show => show.City, city => city.Slug, (show, city) => new { Show = show, city.Name, city.TimeZone })
            .ToDictionaryAsync(item => item.Show.Id, cancellationToken);
        return
        [
            .. ids.Where(shows.ContainsKey).Select(id => shows[id]).Select(item => ProfileShowResponse.From(
                Summary(item.Show),
                new ProfileShowCityResponse(item.Show.City, item.Name, item.TimeZone),
                item.Show.FlyerId)),
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
            .Where(show => show.City == city && show.Slug == slug && show.HiddenAtUtc == null)
            .Join(
                db.Flyers.AsNoTracking(),
                show => show.FlyerId,
                flyer => flyer.Id,
                (show, flyer) => new { Show = show, flyer.FullWidth, flyer.FullHeight })
            .SingleOrDefaultAsync(cancellationToken);
        return found is null ? null : new PublicShowDetails(Details(found.Show), found.FullWidth, found.FullHeight);
    }

    /// <summary>O resumo de show da agenda (SPEC-0008).</summary>
    private static ShowSummaryResponse Summary(Show show) => new(
        show.Id,
        show.Slug,
        show.Title,
        show.StartsAtUtc.UtcDateTime,
        show.Venue.Name,
        [.. show.Lineup.Select(item => item.Name)],
        show.Styles,
        ShowStatusMapping.ToResponse(show.Status),
        ThumbUrl(show.FlyerId),
        AttendingCount: 0);

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
        show.Status,
        show.HiddenAtUtc != null);

    private static string ThumbUrl(string flyerId) => MediaPaths.PublicUrl(Flyer.VariantPath(flyerId, Flyer.Thumb));
}
