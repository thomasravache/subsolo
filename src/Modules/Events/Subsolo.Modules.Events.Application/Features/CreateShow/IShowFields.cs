namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>SPEC-0023 — os campos do show que o cadastro e a edição recebem (mesmas regras nos dois).</summary>
internal interface IShowFields
{
    Guid UserId { get; }

    bool IsAdmin { get; }

    Guid OrganizerProfileId { get; }

    string? Title { get; }

    string? City { get; }

    string? StartsLocal { get; }

    ShowVenueInput? Venue { get; }

    IReadOnlyList<ShowLineupItemInput>? Lineup { get; }

    IReadOnlyList<string>? Styles { get; }

    string? Price { get; }

    string? TicketUrl { get; }

    string? FlyerId { get; }
}
