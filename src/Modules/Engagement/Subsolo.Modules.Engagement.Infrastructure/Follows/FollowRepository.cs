using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Infrastructure.Follows;

/// <summary>SPEC-0027 — follows no schema <c>engagement</c>.</summary>
internal sealed class FollowRepository(EngagementDbContext db) : IFollowRepository
{
    public Task<bool> ExistsAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return db.Follows.AnyAsync(
            follow => follow.UserId == userId && follow.TargetKind == target.Kind && follow.TargetKey == target.Key,
            cancellationToken);
    }

    public void Add(Follow follow) => db.Follows.Add(follow);

    /// <summary>
    /// Uma consulta pelo índice do alvo: <c>(target_kind = 'city' and target_key = any(@cidades)) or (target_kind =
    /// 'profile' and target_key = any(@perfis))</c>, só a coluna <c>user_id</c>.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(
        IReadOnlyCollection<FollowTarget> targets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var cities = KeysOf(targets, FollowTargetKinds.City);
        var profiles = KeysOf(targets, FollowTargetKinds.Profile);
        return await db.Follows.AsNoTracking()
            .Where(follow =>
                (follow.TargetKind == FollowTargetKinds.City && cities.Contains(follow.TargetKey))
                || (follow.TargetKind == FollowTargetKinds.Profile && profiles.Contains(follow.TargetKey)))
            .Select(follow => follow.UserId)
            .ToListAsync(cancellationToken);
    }

    private static List<string> KeysOf(IReadOnlyCollection<FollowTarget> targets, string kind) =>
        [.. targets.Where(target => target.Kind == kind).Select(target => target.Key).Distinct(StringComparer.Ordinal)];
}
