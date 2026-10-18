using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — <c>POST /api/v1/shows</c>: <paramref name="UserId"/> (artista dono do organizador, ou admin quando
/// <paramref name="IsAdmin"/>) cadastra o show do perfil <paramref name="OrganizerProfileId"/>: título (2–100), cidade
/// (slug), início local da cidade (<c>aaaa-mm-ddThh:mm</c>), casa (perfil ou nome 2–80), line-up (1–15: perfil ou nome
/// 1–80), estilos (1–5), preço (1–40), link do ingresso (<c>https</c> ou nulo) e o flyer (SPEC-0022).
/// </summary>
public sealed record CreateShowCommand(
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

/// <summary>SPEC-0023 — a casa como chegou: <c>{ profileId }</c> ou <c>{ name }</c>.</summary>
public sealed record ShowVenueInput(Guid? ProfileId, string? Name);

/// <summary>SPEC-0023 — uma banda do line-up como chegou: <c>{ profileId }</c> ou <c>{ name }</c>.</summary>
public sealed record ShowLineupItemInput(Guid? ProfileId, string? Name);

/// <summary>SPEC-0023 — corpo de 201 (criação) e 200 (edição): o id e o slug.</summary>
public sealed record ShowSaved(Guid Id, string Slug);
