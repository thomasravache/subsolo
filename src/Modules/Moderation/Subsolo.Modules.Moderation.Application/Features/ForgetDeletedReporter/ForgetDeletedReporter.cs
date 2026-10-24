using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Moderation.Application.Features.ForgetDeletedReporter;

/// <summary>
/// SPEC-0033 — o titular excluiu a conta (<see cref="UserDeletedIntegrationEvent"/>, pela inbox): as denúncias dele
/// ficam sem autor (<c>reporter_user_id</c> nulo) e continuam no histórico de moderação (e as pendentes, na fila).
/// Idempotente: uma entrega repetida não muda nada.
/// </summary>
internal sealed partial class ForgetDeletedReporter(IReportsReadModel reports, ILogger<ForgetDeletedReporter> logger)
    : IIntegrationEventHandler<UserDeletedIntegrationEvent>
{
    public async Task Handle(UserDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var forgotten = await reports.ForgetReporterAsync(integrationEvent.UserId, cancellationToken);
        LogForgotten(logger, integrationEvent.UserId, forgotten);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Conta {UserId} excluída: {Count} denúncias ficaram sem autor")]
    private static partial void LogForgotten(ILogger logger, Guid userId, int count);
}
