using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>
/// SPEC-0033 (Emenda 2) — <see cref="IShowHeadlines"/> sobre <c>events.shows</c>: título, cidade, slug, início e casa dos
/// shows pedidos numa consulta só (só essas colunas; SPEC-0037: mais o fuso da cidade), apenas dos encontrados e visíveis (o oculto pela moderação fica de
/// fora); ids repetidos contam uma vez.
/// </summary>
internal sealed class ShowHeadlines(EventsDbContext db) : IShowHeadlines
{
    public async Task<IReadOnlyDictionary<Guid, ShowHeadline>> GetAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        var wanted = showIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, ShowHeadline>();
        }

        return await db.Shows.AsNoTracking()
            .Where(show => wanted.Contains(show.Id) && show.HiddenAtUtc == null)
            .Join(
                db.Cities.AsNoTracking(),
                show => show.City,
                city => city.Slug,
                (show, city) => new { show.Id, show.Title, show.City, show.Slug, show.StartsAtUtc, VenueName = show.Venue.Name, city.TimeZone })
            .ToDictionaryAsync(
                show => show.Id,
                show => new ShowHeadline(show.Id, show.Title, show.City, show.Slug, show.StartsAtUtc, show.VenueName, show.TimeZone),
                ct);
    }
}
