namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — resposta de <c>GET /api/v1/cities/{slug}/agenda</c>: a cidade e os shows do período.</summary>
public sealed record CityAgendaResponse(CityResponse City, IReadOnlyList<ShowSummaryResponse> Shows);

/// <summary>SPEC-0008 — dados públicos da cidade.</summary>
public sealed record CityResponse(string Slug, string Name, string State, string TimeZone);

/// <summary>
/// SPEC-0008 — resumo de um show na agenda (contrato já definido para o épico de shows; vazio nesta spec).
/// <see cref="StartsAt"/> em UTC; <see cref="Status"/> é <c>scheduled</c>, <c>cancelled</c> ou <c>postponed</c>.
/// </summary>
public sealed record ShowSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    DateTime StartsAt,
    string VenueName,
    IReadOnlyList<string> Lineup,
    IReadOnlyList<string> Styles,
    string Status,
    string? FlyerThumbUrl,
    int AttendingCount);
