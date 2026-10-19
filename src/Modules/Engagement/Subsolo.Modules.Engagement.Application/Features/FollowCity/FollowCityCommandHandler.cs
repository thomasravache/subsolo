using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.FollowCity;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0027): as dependências são usadas pela implementação.

/// <summary>
/// SPEC-0027 — cidade que o <see cref="IEventsCatalog"/> confirma → grava o follow (se ainda não existe; seguir de novo
/// não duplica); senão <see cref="EngagementErrors.TargetNotFound"/>.
/// </summary>
internal sealed class FollowCityCommandHandler(
    IEventsCatalog catalog,
    IFollowRepository follows,
    IEngagementUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<FollowCityCommand>
{
    public Task<Result> Handle(FollowCityCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0027: FollowCity ainda não implementado.");
}

#pragma warning restore CS9113
