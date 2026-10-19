using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Infrastructure.Follows;

/// <summary>SPEC-0027 — leituras dos follows (sem rastreamento).</summary>
internal sealed class FollowsReadModel(EngagementDbContext db) : IFollowsReadModel
{
    public async Task<IReadOnlyList<Follow>> ListOfAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Follows.AsNoTracking()
            .Where(follow => follow.UserId == userId)
            .OrderBy(follow => follow.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(FollowTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return db.Follows.CountAsync(
            follow => follow.TargetKind == target.Kind && follow.TargetKey == target.Key, cancellationToken);
    }
}
