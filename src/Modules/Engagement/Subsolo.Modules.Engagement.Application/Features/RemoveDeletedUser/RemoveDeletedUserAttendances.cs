using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.RemoveDeletedUser;

/// <summary>
/// SPEC-0029 — o titular excluiu a conta (<see cref="UserDeletedIntegrationEvent"/>, pela inbox): os "vou" dele são
/// apagados (ADR-0008) e as contagens caem. Idempotente: uma entrega repetida não apaga nada.
/// </summary>
internal sealed partial class RemoveDeletedUserAttendances(
    IAttendancesMaintenance maintenance,
    ILogger<RemoveDeletedUserAttendances> logger)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var deleted = await maintenance.DeleteAllOfAsync(integrationEvent.UserId, cancellationToken);
        LogRemoved(logger, integrationEvent.UserId, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Conta {UserId} excluída: {Deleted} vou apagados")]
    private static partial void LogRemoved(ILogger logger, Guid userId, int deleted);
}
