using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — confere que o usuário é dono do organizador (salvo o admin) e os perfis de casa e de banda, gera o slug
/// (<c>&lt;titulo&gt;-&lt;aaaa-mm-dd&gt;</c>, único na cidade), guarda o início em UTC, liga o flyer e grava o show com o
/// <c>ShowPublished</c> na outbox, numa transação.
/// </summary>
internal sealed class CreateShowCommandHandler : ICommandHandler<CreateShowCommand, ShowSaved>
{
    public Task<Result<ShowSaved>> Handle(CreateShowCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0023: CreateShow ainda não implementado.");
}
