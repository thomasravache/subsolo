using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application.Diagnostics;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.ClaimProfile;

/// <summary>
/// SPEC-0021 — grava a reivindicação pendente; perfil inexistente → <see cref="ProfilesErrors.NotFound"/>; perfil com
/// dono → <see cref="ProfilesErrors.AlreadyOwned"/>; outro pedido do usuário em análise →
/// <see cref="ProfilesErrors.RequestPending"/>. O log leva só os ids (a prova fica fora, ADR-0008).
/// </summary>
internal sealed partial class ClaimProfileCommandHandler(
    IProfileRepository profiles,
    IProfileRequestRepository requests,
    IProfilesUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<ClaimProfileCommandHandler> logger)
    : ICommandHandler<ClaimProfileCommand, ProfileRequestCreated>
{
    public async Task<Result<ProfileRequestCreated>> Handle(ClaimProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var profile = await profiles.GetByIdAsync(command.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.NotFoundError());
        }

        if (profile.OwnerUserId is not null)
        {
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.AlreadyOwnedError());
        }

        if (await requests.HasPendingAsync(command.UserId, cancellationToken))
        {
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.RequestPendingError());
        }

        var request = ProfileRequest.SubmitClaim(command.UserId, profile.Id, command.Proof!.Trim(), clock.GetUtcNow());
        requests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        ProfilesTelemetry.Requested(profile.Type, ProfileRequestKinds.Claim);
        LogClaimed(logger, request.Id, profile.Id, command.UserId);
        return Result.Success(new ProfileRequestCreated(request.Id, request.Status));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reivindicação {RequestId} do perfil {ProfileId} pelo usuário {UserId}")]
    private static partial void LogClaimed(ILogger logger, Guid requestId, Guid profileId, Guid userId);
}
