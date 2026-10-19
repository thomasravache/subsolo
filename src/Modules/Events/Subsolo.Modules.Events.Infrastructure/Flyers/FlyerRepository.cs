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

    /// <summary>
    /// SPEC-0023 (H22-01) — um <c>DELETE … RETURNING</c> só: a condição é conferida pelo banco na hora de apagar (um flyer
    /// ligado ou reenviado depois da leitura não casa mais).
    /// </summary>
    public async Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(
        DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<string>(
                $"""
                DELETE FROM events.flyers
                WHERE attached_at_utc IS NULL AND created_at_utc < {cutoffUtc}
                RETURNING id AS "Value"
                """)
            .ToListAsync(cancellationToken);

    public void Add(Flyer flyer) => db.Flyers.Add(flyer);

    public void Remove(Flyer flyer) => db.Flyers.Remove(flyer);
}
