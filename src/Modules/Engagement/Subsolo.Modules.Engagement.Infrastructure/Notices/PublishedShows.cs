using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Application;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>
/// SPEC-0031 — <see cref="IPublishedShows"/> sobre <c>engagement.published_shows</c>: a gravação é idempotente pela chave
/// (<c>on conflict do nothing</c>: o mesmo evento entregue de novo não muda a linha), sem passar pela unidade de trabalho.
/// </summary>
internal sealed class PublishedShows(EngagementDbContext db) : IPublishedShows
{
    public async Task AddAsync(PublishedShow show, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(show);
        Guid[] profileIds = [.. show.ProfileIds];
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            insert into engagement.published_shows (show_id, city_slug, profile_ids, published_at_utc)
            values ({show.ShowId}, {show.CitySlug}, {profileIds}, {show.PublishedAtUtc})
            on conflict (show_id) do nothing
            """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PublishedShow>> PublishedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        var rows = await db.PublishedShows.AsNoTracking()
            .Where(row => row.PublishedAtUtc >= sinceUtc)
            .OrderBy(row => row.PublishedAtUtc)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new PublishedShow(row.ShowId, row.CitySlug, row.ProfileIds, row.PublishedAtUtc))];
    }

    public Task<int> DeletePublishedBeforeAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken) =>
        db.PublishedShows.Where(row => row.PublishedAtUtc < beforeUtc).ExecuteDeleteAsync(cancellationToken);
}
