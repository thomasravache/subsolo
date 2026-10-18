using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application.Diagnostics;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.ApproveProfileRequest;

/// <summary>
/// SPEC-0021 — pedido novo aprovado: cria o perfil com o dono; reivindicação aprovada: o perfil sem dono ganha o dono.
/// Os dois levantam <c>ProfileOwnerAssignedDomainEvent</c>. Pedido inexistente → <see cref="ProfilesErrors.RequestNotFound"/>;
/// já decidido → <see cref="ProfilesErrors.RequestDecided"/>; perfil que ganhou dono no meio → <see cref="ProfilesErrors.AlreadyOwned"/>.
/// Um pedido novo cujo nome foi ocupado no tipo enquanto esperava (por um perfil criado pelo admin) → <see cref="ProfilesErrors.NameTaken"/>.
/// </summary>
internal sealed partial class ApproveProfileRequestCommandHandler(
    IProfileRepository profiles,
    IProfileRequestRepository requests,
    IProfilesUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<ApproveProfileRequestCommandHandler> logger)
    : ICommandHandler<ApproveProfileRequestCommand, ProfileRequestApproved>
{
    public async Task<Result<ProfileRequestApproved>> Handle(ApproveProfileRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<ProfileRequestApproved>(ProfilesErrors.RequestNotFoundError());
        }

        if (!request.IsPending)
        {
            return Result.Failure<ProfileRequestApproved>(ProfilesErrors.RequestDecidedError());
        }

        var now = clock.GetUtcNow();
        Profile profile;
        if (request.Kind == ProfileRequestKinds.Claim)
        {
            var claimed = request.ProfileId is { } profileId ? await profiles.GetByIdAsync(profileId, cancellationToken) : null;
            if (claimed is null)
            {
                return Result.Failure<ProfileRequestApproved>(ProfilesErrors.NotFoundError());
            }

            if (claimed.OwnerUserId is not null)
            {
                return Result.Failure<ProfileRequestApproved>(ProfilesErrors.AlreadyOwnedError());
            }

            claimed.AssignOwner(request.RequesterUserId);
            profile = claimed;
        }
        else
        {
            var draft = request.Draft ?? throw new InvalidOperationException("Pedido novo sem os dados do perfil.");
            if (await profiles.ExistsAsync(draft.Type, draft.Slug, cancellationToken))
            {
                return Result.Failure<ProfileRequestApproved>(ProfilesErrors.NameTakenError());
            }

            profile = Profile.Create(draft, request.RequesterUserId, now);
            profiles.Add(profile);
        }

        request.Approve(profile.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        ProfilesTelemetry.Decided(ProfileRequestStatuses.Approved);
        LogApproved(logger, request.Id, profile.Id);
        return Result.Success(new ProfileRequestApproved(profile.Id));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido de perfil {RequestId} aprovado: perfil {ProfileId}")]
    private static partial void LogApproved(ILogger logger, Guid requestId, Guid profileId);
}
