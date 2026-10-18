using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application.Diagnostics;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

/// <summary>
/// SPEC-0021 — recusa o pedido pendente com o motivo; inexistente → <see cref="ProfilesErrors.RequestNotFound"/>; já
/// decidido → <see cref="ProfilesErrors.RequestDecided"/>.
/// </summary>
internal sealed partial class RejectProfileRequestCommandHandler(
    IProfileRequestRepository requests,
    IProfilesUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<RejectProfileRequestCommandHandler> logger)
    : ICommandHandler<RejectProfileRequestCommand>
{
    public async Task<Result> Handle(RejectProfileRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(ProfilesErrors.RequestNotFoundError());
        }

        if (!request.IsPending)
        {
            return Result.Failure(ProfilesErrors.RequestDecidedError());
        }

        request.Reject(command.Reason!.Trim(), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        ProfilesTelemetry.Decided(ProfileRequestStatuses.Rejected);
        LogRejected(logger, request.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido de perfil {RequestId} recusado")]
    private static partial void LogRejected(ILogger logger, Guid requestId);
}
