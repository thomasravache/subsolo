using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyFollows;

/// <summary>
/// SPEC-0027 — os follows do fã (do mais antigo para o mais novo) com os nomes das cidades
/// (<see cref="IEventsCatalogNames"/>) e os dados dos perfis (<see cref="IProfilesApi"/>).
/// </summary>
internal sealed class ListMyFollowsQueryHandler(
    IFollowsReadModel follows,
    IEventsCatalogNames catalog,
    IProfilesApi profiles) : IQueryHandler<ListMyFollowsQuery, MyFollowsResponse>
{
    public async Task<Result<MyFollowsResponse>> Handle(ListMyFollowsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var mine = await follows.ListOfAsync(query.UserId, cancellationToken);
        return Result.Success(await new FollowedTargets(catalog, profiles).DescribeAsync(mine, cancellationToken));
    }
}
