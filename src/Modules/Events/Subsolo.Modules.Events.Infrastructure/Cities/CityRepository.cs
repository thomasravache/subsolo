using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Infrastructure.Cities;

/// <summary>SPEC-0008 — leitura de cidades no schema <c>events</c>.</summary>
internal sealed class CityRepository(EventsDbContext db) : ICityRepository
{
    public Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Cities.AsNoTracking().SingleOrDefaultAsync(city => city.Slug == slug, cancellationToken);
}
