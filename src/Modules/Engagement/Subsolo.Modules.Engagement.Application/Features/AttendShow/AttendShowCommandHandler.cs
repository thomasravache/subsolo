using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Engagement.Application.Features.AttendShow;

/// <summary>
/// SPEC-0029 — esqueleto (G1): show agendado e futuro (pelo <c>IEventsCatalog.GetShowStateAsync</c>) → grava o "vou"
/// (sem duplicar); cancelado ou passado → <see cref="EngagementErrors.ShowNotAttendable"/>; inexistente →
/// <see cref="EngagementErrors.ShowNotFound"/>. A implementação entra no G2.
/// </summary>
internal sealed class AttendShowCommandHandler : ICommandHandler<AttendShowCommand>
{
    public Task<Result> Handle(AttendShowCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0029: marcar vou ainda não implementado");
}
