using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Infrastructure.Supporters;

/// <summary>SPEC-0028 — apoiadores no schema <c>site</c>.</summary>
internal sealed class SupporterRepository(SiteDbContext db) : ISupporterRepository
{
    public async Task<IReadOnlyList<Supporter>> ListAsync(CancellationToken cancellationToken) =>
        await db.Supporters.AsNoTracking()
            .OrderBy(supporter => supporter.CreatedAtUtc)
            .ThenBy(supporter => supporter.Id)
            .ToListAsync(cancellationToken);

    public Task<Supporter?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Supporters.FirstOrDefaultAsync(supporter => supporter.Id == id, cancellationToken);

    public void Add(Supporter supporter) => db.Supporters.Add(supporter);

    public void Remove(Supporter supporter) => db.Supporters.Remove(supporter);
}
