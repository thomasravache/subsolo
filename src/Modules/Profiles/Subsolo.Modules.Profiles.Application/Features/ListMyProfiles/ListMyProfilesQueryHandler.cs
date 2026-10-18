using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application.Features.ListMyProfiles;

/// <summary>SPEC-0021 — lê os perfis e os pedidos do usuário.</summary>
internal sealed class ListMyProfilesQueryHandler(IProfilesReadModel profiles) : IQueryHandler<ListMyProfilesQuery, MyProfilesResponse>
{
    public async Task<Result<MyProfilesResponse>> Handle(ListMyProfilesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Result.Success(await profiles.ListMineAsync(query.UserId, cancellationToken));
    }
}
