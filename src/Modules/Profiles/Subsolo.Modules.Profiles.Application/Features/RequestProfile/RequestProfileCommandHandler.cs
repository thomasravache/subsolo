using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application.Features.RequestProfile;

/// <summary>
/// SPEC-0021 — grava o pedido pendente com o slug gerado do nome; outro pedido do usuário em análise →
/// <see cref="ProfilesErrors.RequestPending"/>; perfil do mesmo tipo com o slug → <see cref="ProfilesErrors.NameTaken"/>.
/// </summary>
internal sealed class RequestProfileCommandHandler : ICommandHandler<RequestProfileCommand, ProfileRequestCreated>
{
    public Task<Result<ProfileRequestCreated>> Handle(RequestProfileCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0021: RequestProfile ainda não implementado.");
}
