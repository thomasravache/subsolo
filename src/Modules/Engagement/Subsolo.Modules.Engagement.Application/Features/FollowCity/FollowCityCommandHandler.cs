using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.FollowCity;

/// <summary>
/// SPEC-0027 — cidade que o <see cref="IEventsCatalog"/> confirma → grava o follow (se ainda não existe; seguir de novo
/// não duplica); senão <see cref="EngagementErrors.TargetNotFound"/>.
/// </summary>
internal sealed class FollowCityCommandHandler(
    IEventsCatalog catalog,
    IFollowRepository follows,
    IEngagementUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<FollowCityCommandHandler> logger) : ICommandHandler<FollowCityCommand>
{
    public async Task<Result> Handle(FollowCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.CitySlug) || !await catalog.CityExistsAsync(command.CitySlug, cancellationToken))
        {
            return Result.Failure(EngagementErrors.TargetNotFoundError());
        }

        await FollowWriter.FollowAsync(
            follows, unitOfWork, command.UserId, FollowTarget.City(command.CitySlug), clock.GetUtcNow(), logger, cancellationToken);
        return Result.Success();
    }
}
