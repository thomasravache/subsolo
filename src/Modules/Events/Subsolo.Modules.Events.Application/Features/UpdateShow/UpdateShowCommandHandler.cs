using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.CreateShow;

namespace Subsolo.Modules.Events.Application.Features.UpdateShow;

/// <summary>
/// SPEC-0023 — show inexistente → <c>events.show_not_found</c>; usuário que não é dono do organizador (salvo o admin) →
/// <c>auth.forbidden</c>; senão grava os dados novos (slug igual) com o <c>ShowChanged</c> na outbox.
/// </summary>
internal sealed class UpdateShowCommandHandler : ICommandHandler<UpdateShowCommand, ShowSaved>
{
    public Task<Result<ShowSaved>> Handle(UpdateShowCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0023: UpdateShow ainda não implementado.");
}
