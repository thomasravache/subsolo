using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — resposta de <c>GET /api/v1/cities/{slug}/agenda</c>: a cidade e os shows do período. SPEC-0025 — mais o
/// <see cref="Period"/> (datas locais da cidade) e o <see cref="Total"/> de shows.
/// </summary>
public sealed record CityAgendaResponse(CityResponse City, IReadOnlyList<ShowSummaryResponse> Shows)
{
    /// <summary>SPEC-0025 — o período da agenda, em datas locais da cidade (sem <c>from</c>/<c>to</c>: hoje a hoje + 90).</summary>
    [JsonRequired]
    public AgendaPeriodResponse Period { get; init; }

    /// <summary>SPEC-0025 — quantos shows há no período (com os filtros).</summary>
    [JsonRequired]
    public int Total { get; init; }
}

/// <summary>SPEC-0025 — período da agenda em datas locais da cidade (<c>aaaa-mm-dd</c>), com as duas pontas incluídas.</summary>
public readonly record struct AgendaPeriodResponse(
    [property: JsonRequired] DateOnly From,
    [property: JsonRequired] DateOnly To);

/// <summary>SPEC-0008 — dados públicos da cidade (<see cref="State"/> = UF com 2 letras).</summary>
public sealed record CityResponse(
    string Slug,
    string Name,
    [property: StringLength(2, MinimumLength = 2)] string State,
    string TimeZone);

/// <summary>
/// SPEC-0008 — resumo de um show na agenda (contrato já definido para o épico de shows; vazio nesta spec).
/// <see cref="StartsAt"/> em UTC (SPEC-0023: <see cref="DateTimeKind.Utc"/>, serializado com <c>Z</c>). SPEC-0026: não
/// selado, para a lista de shows do perfil estender o resumo com a cidade
/// (<see cref="ListProfileShows.ProfileShowResponse"/>).
/// </summary>
public record ShowSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    DateTime StartsAt,
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
