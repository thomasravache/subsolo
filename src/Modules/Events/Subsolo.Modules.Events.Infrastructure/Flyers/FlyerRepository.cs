using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Infrastructure.Flyers;

/// <summary>SPEC-0022 — flyers no schema <c>events</c>.</summary>
internal sealed class FlyerRepository(EventsDbContext db) : IFlyerRepository
{
    public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
        db.Flyers.SingleOrDefaultAsync(flyer => flyer.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
        db.Flyers.AnyAsync(flyer => flyer.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(
        DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        await db.Flyers
            .Where(flyer => flyer.AttachedAtUtc == null && flyer.CreatedAtUtc < cutoffUtc)
            .OrderBy(flyer => flyer.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(Flyer flyer) => db.Flyers.Add(flyer);

    public void Remove(Flyer flyer) => db.Flyers.Remove(flyer);
}
