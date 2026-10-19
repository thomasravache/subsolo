using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Application.Features.ListMyShows;

/// <summary>
/// SPEC-0023 — <c>GET /api/v1/me/shows?period=upcoming|past</c>: para o artista, os shows organizados por um perfil dele;
/// para o admin, os que ele cadastrou (Emenda v2, c).
/// </summary>
public sealed record ListMyShowsQuery(Guid UserId, bool IsAdmin, string? Period) : IQuery<IReadOnlyList<MyShowResponse>>
{
    public const string Upcoming = "upcoming";

    public const string Past = "past";
}

/// <summary>
/// SPEC-0023 — um show em "Meus shows" (§6); <see cref="StartsAt"/> em UTC (serializado com <c>Z</c>). SPEC-0024 (H23-06):
/// <see cref="TimeZone"/> é o fuso (IANA) da cidade, para a tela mostrar a hora local.
/// </summary>
public sealed record MyShowResponse(
    Guid Id,
    string Slug,
    string Title,
    DateTime StartsAt,
    string City,
    string TimeZone,
    string VenueName,
    ShowStatus Status,
    string FlyerThumbUrl,
    Guid OrganizerProfileId);
