using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>SPEC-0023 — shows no schema <c>events</c>.</summary>
internal sealed class ShowRepository(EventsDbContext db) : IShowRepository
{
    public Task<Show?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shows.SingleOrDefaultAsync(show => show.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string city, string slug, CancellationToken cancellationToken) =>
        db.Shows.AnyAsync(show => show.City == city && show.Slug == slug, cancellationToken);

    public void Add(Show show) => db.Shows.Add(show);
}
