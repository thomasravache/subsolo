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
/// Emenda 3: aprovações concorrentes têm um só vencedor (token otimista e índice único → 409, nunca 500). Um pedido novo cujo nome foi ocupado no tipo enquanto esperava (por um perfil criado pelo admin) → <see cref="ProfilesErrors.NameTaken"/>.
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
            // O nome tomado no tipo enquanto o pedido esperava aparece na gravação (índice único), e não numa
            // consulta antes: assim a corrida com outra aprovação do mesmo pedido dá request_decided (Emenda 3).
            var draft = request.Draft ?? throw new InvalidOperationException("Pedido novo sem os dados do perfil.");
            profile = Profile.Create(draft, request.RequesterUserId, now);
            profiles.Add(profile);
        }

        request.Approve(profile.Id, now);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ProfilesConflictException conflict)
        {
            return Result.Failure<ProfileRequestApproved>(await LostRaceAsync(command.RequestId, conflict.Conflict, cancellationToken));
        }
        ProfilesTelemetry.Decided(ProfileRequestStatuses.Approved);
        LogApproved(logger, request.Id, profile.Id);
        return Result.Success(new ProfileRequestApproved(profile.Id));
    }

    /// <summary>
    /// Emenda 3 — outra gravação chegou antes: se o pedido já foi decidido, <see cref="ProfilesErrors.RequestDecided"/>;
    /// senão, o perfil ganhou dono (<see cref="ProfilesErrors.AlreadyOwned"/>) ou o nome foi tomado no tipo
    /// (<see cref="ProfilesErrors.NameTaken"/>).
    /// </summary>
    private async Task<Error> LostRaceAsync(Guid requestId, ProfilesConflict conflict, CancellationToken cancellationToken)
    {
        LogLostRace(logger, requestId, conflict);
        var current = await requests.GetByIdAsync(requestId, cancellationToken);
        if (current is null || !current.IsPending)
        {
            return ProfilesErrors.RequestDecidedError();
        }

        return conflict switch
        {
            ProfilesConflict.ProfileChanged => ProfilesErrors.AlreadyOwnedError(),
            ProfilesConflict.NameTaken => ProfilesErrors.NameTakenError(),
            _ => ProfilesErrors.RequestDecidedError(),
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aprovação do pedido {RequestId} perdeu a corrida: {Conflict}")]
    private static partial void LogLostRace(ILogger logger, Guid requestId, ProfilesConflict conflict);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido de perfil {RequestId} aprovado: perfil {ProfileId}")]
    private static partial void LogApproved(ILogger logger, Guid requestId, Guid profileId);
}
