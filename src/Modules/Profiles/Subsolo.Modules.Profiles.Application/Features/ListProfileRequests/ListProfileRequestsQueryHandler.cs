using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Application.Features.ListProfileRequests;

/// <summary>
/// SPEC-0021 — lê os pedidos pendentes e completa com o e-mail de quem pediu pela interface pública do Users
/// (<see cref="IUsersDirectory"/>, Emenda v2), numa só consulta.
/// </summary>
internal sealed class ListProfileRequestsQueryHandler(IProfilesReadModel profiles, IUsersDirectory users)
    : IQueryHandler<ListProfileRequestsQuery, IReadOnlyList<ProfileRequestQueueItem>>
{
    public async Task<Result<IReadOnlyList<ProfileRequestQueueItem>>> Handle(
        ListProfileRequestsQuery query, CancellationToken cancellationToken)
    {
        var pending = await profiles.ListPendingAsync(cancellationToken);
        IReadOnlyDictionary<Guid, string> emails = pending.Count == 0
            ? new Dictionary<Guid, string>()
            : await users.GetEmailsAsync([.. pending.Select(request => request.RequesterUserId).Distinct()], cancellationToken);

        IReadOnlyList<ProfileRequestQueueItem> items =
        [
            .. pending.Select(request => new ProfileRequestQueueItem(
                request.RequestId,
                request.Kind,
                request.Type,
                request.Name,
                request.City,
                request.Styles,
                request.Links,
                request.ContactOrProof,
                emails.GetValueOrDefault(request.RequesterUserId),
                request.CreatedAtUtc,
                request.ProfileId)),
        ];
        return Result.Success(items);
    }
}
