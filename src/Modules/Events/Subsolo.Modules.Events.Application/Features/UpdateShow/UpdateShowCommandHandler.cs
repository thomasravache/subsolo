using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.UpdateShow;

/// <summary>
/// SPEC-0023 — show inexistente → <c>events.show_not_found</c>; usuário que não é dono do organizador (salvo o admin) →
/// <c>auth.forbidden</c>; senão grava os dados novos (slug igual) com o <c>ShowChanged</c> na outbox.
/// </summary>
internal sealed partial class UpdateShowCommandHandler(
    IShowRepository shows,
    IEventsUnitOfWork unitOfWork,
    ICityRepository cities,
    IFlyerCatalog flyers,
    IShowProfiles profiles,
    TimeProvider clock,
    ILogger<UpdateShowCommandHandler> logger)
    : ICommandHandler<UpdateShowCommand, ShowSaved>
{
    private readonly ShowInputResolver _resolver = new(profiles, cities, flyers);

    public async Task<Result<ShowSaved>> Handle(UpdateShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var show = await shows.GetByIdAsync(command.ShowId, cancellationToken);
        if (show is null)
        {
            return Result.Failure<ShowSaved>(EventsErrors.ShowNotFoundError());
        }

        // Dono do organizador atual (e, pelo resolvedor, do novo, se mudar).
        var current = await ShowInputResolver.AuthorizeAsync(profiles, show.OrganizerProfileId, command.UserId, command.IsAdmin, cancellationToken);
        if (current.IsFailure)
        {
            return Result.Failure<ShowSaved>(current.Error);
        }

        var flyerChanged = !string.Equals(show.FlyerId, command.FlyerId, StringComparison.Ordinal);
        var resolved = await _resolver.ResolveAsync(command, flyerChanged, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<ShowSaved>(resolved.Error);
        }

        var data = resolved.Value;
        if (data.City != show.City && await shows.SlugExistsAsync(data.City, show.Slug, cancellationToken))
        {
            return Result.Failure<ShowSaved>(EventsErrors.ShowSlugTakenError());
        }

        show.Update(data, clock.GetUtcNow());
        if (flyerChanged)
        {
            await flyers.AttachAsync(data.FlyerId, cancellationToken);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateSlugException)
        {
            return Result.Failure<ShowSaved>(EventsErrors.ShowSlugTakenError());
        }
        catch (MissingFlyerException)
        {
            // SPEC-0024 (H23-02): a limpeza apagou o flyer entre a conferência e a gravação.
            return Result.Failure<ShowSaved>(EventsErrors.FlyerNotFoundError());
        }

        LogShowUpdated(logger, command.UserId, show.Id);
        return Result.Success(new ShowSaved(show.Id, show.Slug));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: usuário {UserId} editou o show {ShowId}")]
    private static partial void LogShowUpdated(ILogger logger, Guid userId, Guid showId);
}
