using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Diagnostics;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — confere que o usuário é dono do organizador (salvo o admin) e os perfis de casa e de banda, gera o slug
/// (<c>&lt;titulo&gt;-&lt;aaaa-mm-dd&gt;</c>, único na cidade), guarda o início em UTC, liga o flyer e grava o show com o
/// <c>ShowPublished</c> na outbox, numa transação.
/// </summary>
internal sealed partial class CreateShowCommandHandler(
    IShowRepository shows,
    IEventsUnitOfWork unitOfWork,
    ICityRepository cities,
    IFlyerCatalog flyers,
    IShowProfiles profiles,
    TimeProvider clock,
    ILogger<CreateShowCommandHandler> logger)
    : ICommandHandler<CreateShowCommand, ShowSaved>
{
    private readonly ShowInputResolver _resolver = new(profiles, cities, flyers);

    public async Task<Result<ShowSaved>> Handle(CreateShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var started = Stopwatch.GetTimestamp();
        var resolved = await _resolver.ResolveAsync(command, flyerChanged: true, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<ShowSaved>(resolved.Error);
        }

        var data = resolved.Value;
        var city = await cities.GetBySlugAsync(data.City, cancellationToken);
        var slug = ShowInputResolver.SlugOf(data.Title, data.StartsAtUtc, city!.TimeZone);
        if (await shows.SlugExistsAsync(data.City, slug, cancellationToken))
        {
            return Result.Failure<ShowSaved>(EventsErrors.ShowSlugTakenError());
        }

        var show = Show.Create(slug, data, command.UserId, clock.GetUtcNow());
        shows.Add(show);
        await flyers.AttachAsync(data.FlyerId, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateSlugException)
        {
            // Outro cadastro gravou o mesmo slug na cidade entre a conferência e a gravação.
            return Result.Failure<ShowSaved>(EventsErrors.ShowSlugTakenError());
        }
        catch (MissingFlyerException)
        {
            // SPEC-0024 (H23-02): a limpeza apagou o flyer entre a conferência e a gravação.
            return Result.Failure<ShowSaved>(EventsErrors.FlyerNotFoundError());
        }

        EventsTelemetry.ShowCreated(Stopwatch.GetElapsedTime(started));
        LogShowCreated(logger, command.UserId, show.Id, show.OrganizerProfileId);
        return Result.Success(new ShowSaved(show.Id, show.Slug));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: usuário {UserId} cadastrou o show {ShowId} do perfil {ProfileId}")]
    private static partial void LogShowCreated(ILogger logger, Guid userId, Guid showId, Guid profileId);
}
