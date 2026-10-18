using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application.Features.ChangeShowStatus;

/// <summary>
/// SPEC-0023 — situação desconhecida → <c>events.invalid_status</c>; show inexistente → <c>events.show_not_found</c>;
/// não dono (salvo o admin) → <c>auth.forbidden</c>; senão grava a situação com o <c>ShowChanged</c> na outbox.
/// </summary>
internal sealed class ChangeShowStatusCommandHandler : ICommandHandler<ChangeShowStatusCommand>
{
    public Task<Result> Handle(ChangeShowStatusCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0023: ChangeShowStatus ainda não implementado.");
}
