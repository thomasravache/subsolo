using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — resposta de <c>GET /api/v1/cities/{slug}/agenda</c>: a cidade e os shows do período.</summary>
public sealed record CityAgendaResponse(CityResponse City, IReadOnlyList<ShowSummaryResponse> Shows);

/// <summary>SPEC-0008 — dados públicos da cidade (<see cref="State"/> = UF com 2 letras).</summary>
public sealed record CityResponse(
    string Slug,
    string Name,
    [property: StringLength(2, MinimumLength = 2)] string State,
    string TimeZone);

/// <summary>
/// SPEC-0008 — resumo de um show na agenda (contrato já definido para o épico de shows; vazio nesta spec).
/// <see cref="StartsAt"/> em UTC.
/// </summary>
public sealed record ShowSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    DateTimeOffset StartsAt,
    string VenueName,
    IReadOnlyList<string> Lineup,
    IReadOnlyList<string> Styles,
    ShowStatus Status,
    string? FlyerThumbUrl,
    int AttendingCount);

/// <summary>SPEC-0008 — situação do show, serializada como <c>scheduled</c>, <c>cancelled</c> ou <c>postponed</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShowStatus>))]
public enum ShowStatus
{
    [JsonStringEnumMemberName("scheduled")]
    Scheduled,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    [JsonStringEnumMemberName("postponed")]
    Postponed,
}
