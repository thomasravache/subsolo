using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application.Diagnostics;
using Subsolo.Modules.Profiles.Application.Features.Shared;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.RequestProfile;

/// <summary>
/// SPEC-0021 — grava o pedido pendente com o slug gerado do nome; outro pedido do usuário em análise →
/// <see cref="ProfilesErrors.RequestPending"/>; perfil do mesmo tipo com o slug → <see cref="ProfilesErrors.NameTaken"/>.
/// O log leva só os ids (o contato fica fora, ADR-0008).
/// </summary>
internal sealed partial class RequestProfileCommandHandler(
    IProfileRepository profiles,
    IProfileRequestRepository requests,
    IProfilesUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<RequestProfileCommandHandler> logger)
    : ICommandHandler<RequestProfileCommand, ProfileRequestCreated>
{
    public async Task<Result<ProfileRequestCreated>> Handle(RequestProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await requests.HasPendingAsync(command.UserId, cancellationToken))
        {
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.RequestPendingError());
        }

        var draft = command.ToDraft();
        if (await profiles.ExistsAsync(draft.Type, draft.Slug, cancellationToken))
        {
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.NameTakenError());
        }

        var request = ProfileRequest.SubmitNew(command.UserId, draft, command.Contact!.Trim(), clock.GetUtcNow());
        requests.Add(request);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ProfilesConflictException conflict) when (conflict.Conflict == ProfilesConflict.RequestPending)
        {
            // Emenda 3: outro pedido do mesmo usuário foi gravado no meio (índice único parcial dos pendentes).
            return Result.Failure<ProfileRequestCreated>(ProfilesErrors.RequestPendingError());
        }
        ProfilesTelemetry.Requested(draft.Type, ProfileRequestKinds.New);
        LogRequested(logger, request.Id, command.UserId);
        return Result.Success(new ProfileRequestCreated(request.Id, request.Status));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido de perfil {RequestId} do usuário {UserId}")]
    private static partial void LogRequested(ILogger logger, Guid requestId, Guid userId);
}
