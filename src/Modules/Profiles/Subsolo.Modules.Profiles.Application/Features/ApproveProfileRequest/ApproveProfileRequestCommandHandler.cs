using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application.Features.ApproveProfileRequest;

/// <summary>
/// SPEC-0021 — pedido novo aprovado: cria o perfil com o dono; reivindicação aprovada: o perfil sem dono ganha o dono.
/// Os dois levantam <c>ProfileOwnerAssignedDomainEvent</c>. Pedido inexistente → <see cref="ProfilesErrors.RequestNotFound"/>;
/// já decidido → <see cref="ProfilesErrors.RequestDecided"/>; perfil que ganhou dono no meio → <see cref="ProfilesErrors.AlreadyOwned"/>.
/// </summary>
internal sealed class ApproveProfileRequestCommandHandler : ICommandHandler<ApproveProfileRequestCommand, ProfileRequestApproved>
{
    public Task<Result<ProfileRequestApproved>> Handle(ApproveProfileRequestCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0021: ApproveProfileRequest ainda não implementado.");
}
