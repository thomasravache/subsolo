using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

/// <summary>
/// SPEC-0021 — recusa o pedido pendente com o motivo; inexistente → <see cref="ProfilesErrors.RequestNotFound"/>; já
/// decidido → <see cref="ProfilesErrors.RequestDecided"/>.
/// </summary>
internal sealed class RejectProfileRequestCommandHandler : ICommandHandler<RejectProfileRequestCommand>
{
    public Task<Result> Handle(RejectProfileRequestCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0021: RejectProfileRequest ainda não implementado.");
}
