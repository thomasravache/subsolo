using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Infrastructure.Follows;

/// <summary>SPEC-0027 — remoções em lote direto no banco (deixar de seguir e exclusão da conta).</summary>
internal sealed class FollowsMaintenance(EngagementDbContext db) : IFollowsMaintenance
{
    public Task<int> DeleteAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return db.Follows
            .Where(follow => follow.UserId == userId && follow.TargetKind == target.Kind && follow.TargetKey == target.Key)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAllOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Follows.Where(follow => follow.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
