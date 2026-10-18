using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.CreateShow;

namespace Subsolo.Modules.Events.Application.Features.UpdateShow;

/// <summary>
/// SPEC-0023 — <c>PUT /api/v1/shows/{id}</c>: as mesmas regras do <see cref="CreateShowCommand"/>; o slug não muda.
/// </summary>
public sealed record UpdateShowCommand(
    Guid ShowId,
    Guid UserId,
    bool IsAdmin,
    Guid OrganizerProfileId,
    string? Title,
    string? City,
    string? StartsLocal,
    ShowVenueInput? Venue,
    IReadOnlyList<ShowLineupItemInput>? Lineup,
    IReadOnlyList<string>? Styles,
    string? Price,
    string? TicketUrl,
    string? FlyerId) : ICommand<ShowSaved>;
