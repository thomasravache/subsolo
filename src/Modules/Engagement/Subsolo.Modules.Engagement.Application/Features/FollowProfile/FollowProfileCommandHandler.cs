using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.FollowProfile;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0027): as dependências são usadas pela implementação.

/// <summary>
/// SPEC-0027 — perfil aprovado (o <see cref="IProfilesApi"/> o encontra) → grava o follow (se ainda não existe);
/// inexistente ou não aprovado → <see cref="EngagementErrors.TargetNotFound"/>.
/// </summary>
internal sealed class FollowProfileCommandHandler(
    IProfilesApi profiles,
    IFollowRepository follows,
    IEngagementUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<FollowProfileCommand>
{
    public Task<Result> Handle(FollowProfileCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0027: FollowProfile ainda não implementado.");
}

#pragma warning restore CS9113
