using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.HideShow;

/// <summary>
/// SPEC-0033 — a moderação ocultou o show (<see cref="ShowHiddenByModerationIntegrationEvent"/>, pela inbox): grava
/// <c>hidden_at_utc</c> e o show sai da agenda, da página, dos shows do perfil e do <c>IEventsCatalog</c>. Idempotente:
/// ocultar de novo mantém o primeiro instante; show inexistente é ignorado.
/// </summary>
internal sealed partial class HideShowOnModeration(
    IShowRepository shows,
    IEventsUnitOfWork unitOfWork,
    ILogger<HideShowOnModeration> logger)
    : IIntegrationEventHandler<ShowHiddenByModerationIntegrationEvent>
{
    public async Task Handle(ShowHiddenByModerationIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var show = await shows.GetByIdAsync(integrationEvent.ShowId, cancellationToken);
        if (show is null)
        {
            LogShowMissing(logger, integrationEvent.ShowId);
            return;
        }

        if (!show.HideByModeration(integrationEvent.HiddenAtUtc))
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogShowHidden(logger, show.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: show {ShowId} oculto pela moderação")]
    private static partial void LogShowHidden(ILogger logger, Guid showId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Show {ShowId} oculto pela moderação não existe no Events")]
    private static partial void LogShowMissing(ILogger logger, Guid showId);
}
