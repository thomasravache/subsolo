using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetFollowers;

/// <summary>
/// SPEC-0027 — <see cref="IEngagementApi"/>: quem segue a cidade ou algum dos perfis, pelos follows de
/// <see cref="IFollowRepository.GetFollowerIdsAsync"/> (uma consulta só, pelo índice do alvo), sem repetição.
/// </summary>
internal sealed class EngagementApi(IFollowRepository follows) : IEngagementApi
{
    public async Task<IReadOnlyCollection<Guid>> GetFollowersAsync(
        string citySlug, IReadOnlyCollection<Guid> profileIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileIds);
        List<FollowTarget> targets = [.. profileIds.Distinct().Select(FollowTarget.Profile)];
        if (!string.IsNullOrWhiteSpace(citySlug))
        {
            targets.Add(FollowTarget.City(citySlug));
        }

        if (targets.Count == 0)
        {
            return [];
        }

        var ids = await follows.GetFollowerIdsAsync(targets, ct);
        return ids.ToHashSet();
    }
}
