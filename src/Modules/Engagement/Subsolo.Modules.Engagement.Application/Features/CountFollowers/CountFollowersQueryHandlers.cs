using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.CountFollowers;

/// <summary>SPEC-0027 — cidade existente (<see cref="IEventsCatalog"/>) → a contagem; senão <see cref="EngagementErrors.TargetNotFound"/>.</summary>
internal sealed class CountCityFollowersQueryHandler(IEventsCatalog catalog, IFollowsReadModel follows)
    : IQueryHandler<CountCityFollowersQuery, FollowerCountResponse>
{
    public async Task<Result<FollowerCountResponse>> Handle(CountCityFollowersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.CitySlug) || !await catalog.CityExistsAsync(query.CitySlug, cancellationToken))
        {
            return Result.Failure<FollowerCountResponse>(EngagementErrors.TargetNotFoundError());
        }

        return Result.Success(new FollowerCountResponse(
            await follows.CountAsync(FollowTarget.City(query.CitySlug), cancellationToken)));
    }
}

/// <summary>
/// SPEC-0027 — perfil aprovado (<see cref="IProfilesApi"/>) → a contagem; inexistente ou pendente →
/// <see cref="EngagementErrors.TargetNotFound"/>.
/// </summary>
internal sealed class CountProfileFollowersQueryHandler(IProfilesApi profiles, IFollowsReadModel follows)
    : IQueryHandler<CountProfileFollowersQuery, FollowerCountResponse>
{
    public async Task<Result<FollowerCountResponse>> Handle(CountProfileFollowersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await profiles.GetAsync(query.ProfileId, cancellationToken) is null)
        {
            return Result.Failure<FollowerCountResponse>(EngagementErrors.TargetNotFoundError());
        }

        return Result.Success(new FollowerCountResponse(
            await follows.CountAsync(FollowTarget.Profile(query.ProfileId), cancellationToken)));
    }
}
