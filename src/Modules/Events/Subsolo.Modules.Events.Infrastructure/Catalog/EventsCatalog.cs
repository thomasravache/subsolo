using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>
/// SPEC-0021 — implementação de <see cref="IEventsCatalog"/> sobre as cidades e estilos do schema <c>events</c>. SPEC-0026:
/// e de <see cref="IEventsCatalogNames"/> (os nomes). SPEC-0031: e o resumo público de um show, para os avisos.
/// </summary>
internal sealed class EventsCatalog(EventsDbContext db) : IEventsCatalog, IEventsCatalogNames
{
    public async Task<CatalogNames> GetNamesAsync(
        IReadOnlyCollection<string> citySlugs,
        IReadOnlyCollection<string> styleSlugs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(citySlugs);
        ArgumentNullException.ThrowIfNull(styleSlugs);
        var cities = citySlugs.Distinct(StringComparer.Ordinal).ToList();
        var styles = styleSlugs.Distinct(StringComparer.Ordinal).ToList();
        var cityNames = cities.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await db.Cities.AsNoTracking()
                .Where(city => cities.Contains(city.Slug))
                .ToDictionaryAsync(city => city.Slug, city => city.Name, StringComparer.Ordinal, ct);
        var styleNames = styles.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await db.Styles.AsNoTracking()
                .Where(style => styles.Contains(style.Slug))
                .ToDictionaryAsync(style => style.Slug, style => style.Name, StringComparer.Ordinal, ct);
        return new CatalogNames(cityNames, styleNames);
    }

    public Task<bool> CityExistsAsync(string slug, CancellationToken ct) =>
        db.Cities.AsNoTracking().AnyAsync(city => city.Slug == slug, ct);

    public async Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slugs);
        var wanted = slugs.Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return true;
        }

        var found = await db.Styles.AsNoTracking().CountAsync(style => wanted.Contains(style.Slug), ct);
        return found == wanted.Count;
    }

    /// <summary>SPEC-0029 — a situação e o início do show (só essas duas colunas), ou <see langword="null"/> se não existe.</summary>
    public async Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct) =>
        await db.Shows.AsNoTracking()
            .Where(show => show.Id == showId)
            .Select(show => new ShowState(show.Status, show.StartsAtUtc))
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// SPEC-0031 — o resumo público do show (com o nome e o fuso da cidade e os nomes do line-up), ou
    /// <see langword="null"/> se não existe.
    /// </summary>
    public async Task<ShowSummary?> GetShowSummaryAsync(Guid showId, CancellationToken ct)
    {
        var found = await db.Shows.AsNoTracking()
            .Where(show => show.Id == showId)
            .Join(db.Cities.AsNoTracking(), show => show.City, city => city.Slug, (show, city) => new { Show = show, city.Name, city.TimeZone })
            .SingleOrDefaultAsync(ct);
        return found is null
            ? null
            : new ShowSummary(
                found.Show.Id,
                found.Show.Slug,
                found.Show.City,
                found.Name,
                found.TimeZone,
                [.. found.Show.Lineup.Select(item => item.Name)],
                found.Show.Venue.Name,
                found.Show.StartsAtUtc,
                found.Show.Status);
    }
}
