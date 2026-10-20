using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.FollowProfile;

/// <summary>
/// SPEC-0027 — perfil aprovado (o <see cref="IProfilesApi"/> o encontra: pedido pendente ou recusado não é perfil) →
/// grava o follow (se ainda não existe); inexistente ou não aprovado → <see cref="EngagementErrors.TargetNotFound"/>.
/// </summary>
internal sealed class FollowProfileCommandHandler(
    IProfilesApi profiles,
    IFollowRepository follows,
    IEngagementUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<FollowProfileCommandHandler> logger) : ICommandHandler<FollowProfileCommand>
{
    public async Task<Result> Handle(FollowProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await profiles.GetAsync(command.ProfileId, cancellationToken) is null)
        {
            return Result.Failure(EngagementErrors.TargetNotFoundError());
        }

        await FollowWriter.FollowAsync(
            follows, unitOfWork, command.UserId, FollowTarget.Profile(command.ProfileId), clock.GetUtcNow(), logger, cancellationToken);
        return Result.Success();
    }
}
