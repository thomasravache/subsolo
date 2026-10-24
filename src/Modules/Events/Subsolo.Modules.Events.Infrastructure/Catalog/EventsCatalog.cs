using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>
/// SPEC-0021 — implementação de <see cref="IEventsCatalog"/> sobre as cidades e estilos do schema <c>events</c>. SPEC-0026:
/// e de <see cref="IEventsCatalogNames"/> (os nomes). SPEC-0031: e o resumo público de um show, para os avisos.
/// SPEC-0032: e os shows agendados por início (lembrete na véspera) e as situações em lote. SPEC-0033 (Emenda 1): o show
/// oculto pela moderação é tratado como inexistente em todas as leituras de show (sem aviso, resumo nem lembrete).
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
            .Where(show => show.Id == showId && show.HiddenAtUtc == null)
            .Select(show => new ShowState(show.Status, show.StartsAtUtc))
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// SPEC-0031 — o resumo público do show (com o nome e o fuso da cidade e os nomes do line-up), ou
    /// <see langword="null"/> se não existe.
    /// </summary>
    public async Task<ShowSummary?> GetShowSummaryAsync(Guid showId, CancellationToken ct)
    {
        var found = await db.Shows.AsNoTracking()
            .Where(show => show.Id == showId && show.HiddenAtUtc == null)
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

    /// <summary>
    /// SPEC-0032 — os resumos dos shows <see cref="ShowStatuses.Scheduled"/> com início em [<paramref name="fromUtc"/>,
    /// <paramref name="toUtc"/>), pelo início, numa consulta só (com o nome e o fuso da cidade).
    /// </summary>
    public async Task<IReadOnlyList<ShowSummary>> ListScheduledShowsStartingAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        var found = await db.Shows.AsNoTracking()
            .Where(show => show.Status == ShowStatuses.Scheduled && show.HiddenAtUtc == null)
            .Where(show => show.StartsAtUtc >= fromUtc && show.StartsAtUtc < toUtc)
            .Join(db.Cities.AsNoTracking(), show => show.City, city => city.Slug, (show, city) => new { Show = show, city.Name, city.TimeZone })
            .OrderBy(row => row.Show.StartsAtUtc)
            .ThenBy(row => row.Show.Id)
            .ToListAsync(ct);
        return
        [
            .. found.Select(row => new ShowSummary(
                row.Show.Id,
                row.Show.Slug,
                row.Show.City,
                row.Name,
                row.TimeZone,
                [.. row.Show.Lineup.Select(item => item.Name)],
                row.Show.Venue.Name,
                row.Show.StartsAtUtc,
                row.Show.Status)),
        ];
    }

    /// <summary>
    /// SPEC-0032 (H29-01) — a situação e o início dos shows <paramref name="showIds"/> numa consulta só (só essas colunas),
    /// apenas dos encontrados; ids repetidos contam uma vez.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ShowState>> GetShowStatesAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        var wanted = showIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, ShowState>();
        }

        return await db.Shows.AsNoTracking()
            .Where(show => wanted.Contains(show.Id) && show.HiddenAtUtc == null)
            .Select(show => new { show.Id, show.Status, show.StartsAtUtc })
            .ToDictionaryAsync(show => show.Id, show => new ShowState(show.Status, show.StartsAtUtc), ct);
    }
}
