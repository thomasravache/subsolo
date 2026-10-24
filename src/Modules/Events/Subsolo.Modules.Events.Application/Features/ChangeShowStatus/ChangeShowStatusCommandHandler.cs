using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Diagnostics;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.ChangeShowStatus;

/// <summary>
/// SPEC-0023 — situação desconhecida → <c>events.invalid_status</c>; show inexistente → <c>events.show_not_found</c>;
/// não dono (salvo o admin) → <c>auth.forbidden</c>; oculto pela moderação → <c>events.show_hidden</c> (SPEC-0033); senão grava a situação com o <c>ShowChanged</c> na outbox.
/// </summary>
internal sealed partial class ChangeShowStatusCommandHandler(
    IShowRepository shows,
    IEventsUnitOfWork unitOfWork,
    IShowProfiles profiles,
    TimeProvider clock,
    ILogger<ChangeShowStatusCommandHandler> logger)
    : ICommandHandler<ChangeShowStatusCommand>
{
    public async Task<Result> Handle(ChangeShowStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Status is not { } status || !ShowStatuses.All.Contains(status, StringComparer.Ordinal))
        {
            return Result.Failure(EventsErrors.InvalidStatusError());
        }

        var show = await shows.GetByIdAsync(command.ShowId, cancellationToken);
        if (show is null)
        {
            return Result.Failure(EventsErrors.ShowNotFoundError());
        }

        var authorized = await ShowInputResolver.AuthorizeAsync(profiles, show.OrganizerProfileId, command.UserId, command.IsAdmin, cancellationToken);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        // SPEC-0033: show oculto pela moderação não muda mais de situação.
        if (show.IsHidden)
        {
            return Result.Failure(EventsErrors.ShowHiddenError());
        }

        if (show.Status == status)
        {
            return Result.Success();
        }

        show.ChangeStatus(status, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        EventsTelemetry.ShowStatusChanged(status);
        LogStatusChanged(logger, command.UserId, show.Id, status);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: usuário {UserId} mudou o show {ShowId} para {Status}")]
    private static partial void LogStatusChanged(ILogger logger, Guid userId, Guid showId, string status);
}
