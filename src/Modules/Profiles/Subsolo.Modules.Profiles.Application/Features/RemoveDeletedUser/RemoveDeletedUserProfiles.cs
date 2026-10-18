using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Application.Features.RemoveDeletedUser;

/// <summary>
/// SPEC-0021 — o titular excluiu a conta (<see cref="UserDeletedIntegrationEvent"/>, pela inbox): os perfis dele ficam
/// sem dono (o histórico de shows é da cena e o perfil volta a ser reivindicável) e os pedidos dele, com o contato ou a
/// prova, são apagados (ADR-0008). Idempotente: uma entrega repetida não muda nada.
/// </summary>
internal sealed partial class RemoveDeletedUserProfiles(
    IProfilesMaintenance maintenance,
    ILogger<RemoveDeletedUserProfiles> logger)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var released = await maintenance.ReleaseOwnedProfilesAsync(integrationEvent.UserId, cancellationToken);
        var deleted = await maintenance.DeleteRequestsOfAsync(integrationEvent.UserId, cancellationToken);
        LogRemoved(logger, integrationEvent.UserId, released, deleted);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Conta {UserId} excluída: {Released} perfis ficaram sem dono e {Deleted} pedidos foram apagados")]
    private static partial void LogRemoved(ILogger logger, Guid userId, int released, int deleted);
}
