using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetFollowers;

/// <summary>
/// SPEC-0027 — <see cref="IEngagementApi"/>: quem segue a cidade ou algum dos perfis, pelos follows de
/// <see cref="IFollowRepository.GetFollowerIdsAsync"/> (uma consulta só, pelo índice do alvo), sem repetição. SPEC-0029
/// (H27-09): só contam os perfis que o <see cref="IProfilesApi"/> ainda encontra (um perfil excluído ou que deixou de
/// ser aprovado não leva seus seguidores ao aviso).
/// </summary>
internal sealed class EngagementApi(IFollowRepository follows, IProfilesApi profiles) : IEngagementApi
{
    public async Task<IReadOnlyCollection<Guid>> GetFollowersAsync(
        string citySlug, IReadOnlyCollection<Guid> profileIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileIds);
        var wanted = profileIds.Distinct().ToList();
        var existing = wanted.Count > 0
            ? await profiles.GetManyAsync(wanted, ct)
            : new Dictionary<Guid, ProfileSummary>();
        List<FollowTarget> targets = [.. wanted.Where(existing.ContainsKey).Select(FollowTarget.Profile)];
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
