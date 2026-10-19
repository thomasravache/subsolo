using System.Globalization;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyFollows;

/// <summary>
/// SPEC-0027 — os alvos de uma lista de follows com o nome e o endereço, pelas interfaces públicas do Events
/// (<see cref="IEventsCatalogNames"/>) e do Profiles (<see cref="IProfilesApi"/>); alvos que não existem mais (cidade
/// removida, perfil apagado) ficam de fora.
/// </summary>
internal sealed class FollowedTargets(IEventsCatalogNames catalog, IProfilesApi profiles)
{
    public async Task<MyFollowsResponse> DescribeAsync(IReadOnlyList<Follow> follows, CancellationToken cancellationToken)
    {
        var citySlugs = follows.Where(follow => follow.TargetKind == FollowTargetKinds.City).Select(follow => follow.TargetKey).ToList();
        var names = citySlugs.Count > 0
            ? (await catalog.GetNamesAsync(citySlugs, [], cancellationToken)).Cities
            : new Dictionary<string, string>(StringComparer.Ordinal);
        List<FollowedCityResponse> cities =
        [
            .. citySlugs
                .Where(names.ContainsKey)
                .Select(slug => new FollowedCityResponse(slug, names[slug])),
        ];

        List<FollowedProfileResponse> followed = [];
        foreach (var follow in follows.Where(follow => follow.TargetKind == FollowTargetKinds.Profile))
        {
            if (!Guid.TryParse(follow.TargetKey, CultureInfo.InvariantCulture, out var id)
                || await profiles.GetAsync(id, cancellationToken) is not { } profile)
            {
                continue;
            }

            followed.Add(new FollowedProfileResponse(profile.Id, profile.Type, profile.Name, profile.Slug));
        }

        return new MyFollowsResponse(cities, followed);
    }
}
