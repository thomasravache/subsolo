using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>SPEC-0021 — implementação de <see cref="IEventsCatalog"/> sobre as cidades e estilos do schema <c>events</c>.</summary>
internal sealed class EventsCatalog(EventsDbContext db) : IEventsCatalog
{
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
}
