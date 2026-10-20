using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>
/// SPEC-0021 — implementação de <see cref="IEventsCatalog"/> sobre as cidades e estilos do schema <c>events</c>. SPEC-0026:
/// e de <see cref="IEventsCatalogNames"/> (os nomes).
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
}
