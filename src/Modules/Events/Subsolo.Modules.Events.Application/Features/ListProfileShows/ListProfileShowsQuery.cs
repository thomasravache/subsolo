using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Application.Features.ListProfileShows;

/// <summary>
/// SPEC-0026 — <c>GET /api/v1/shows?profileId={id}&amp;period=upcoming|past</c> (público): os shows em que o perfil é
/// organizador, casa ou banda do line-up; os próximos por data crescente (até <see cref="UpcomingLimit"/>), os passados
/// por data decrescente (até <see cref="PastLimit"/>), com o resumo de show da agenda (SPEC-0008).
/// </summary>
public sealed record ListProfileShowsQuery(Guid ProfileId, string? Period) : IQuery<ProfileShowsResponse>
{
    public const string Upcoming = "upcoming";

    public const string Past = "past";

    /// <summary>Máximo de próximos shows.</summary>
    public const int UpcomingLimit = 50;

    /// <summary>Máximo de shows passados.</summary>
    public const int PastLimit = 20;
}

/// <summary>
/// SPEC-0026 — os shows do perfil, na ordem e no limite do período. A leitura (<see cref="IShowsReadModel"/>) devolve
/// cada item como <see cref="ProfileShowResponse"/> (o resumo mais a cidade); o endpoint publica essa forma. SPEC-0031
/// (H26-01): tipado como <see cref="ProfileShowResponse"/> de ponta a ponta, sem conversão no endpoint.
/// </summary>
public sealed record ProfileShowsResponse(IReadOnlyList<ProfileShowResponse> Shows);

/// <summary>
/// SPEC-0026 (Emenda v2, b) — um show da lista do perfil: o resumo da agenda (SPEC-0008) mais a cidade do show, para o
/// link <c>/{cidade}/{show}</c> e a data no fuso dela. SPEC-0031 (H26-03b): mais o <see cref="FlyerId"/> (o id de 64
/// caracteres do envio; nulo sem flyer), com que a página do perfil monta a imagem Open Graph.
/// </summary>
public sealed record ProfileShowResponse(
    Guid Id,
    string Slug,
    string Title,
    DateTime StartsAt,
    string VenueName,
    IReadOnlyList<string> Lineup,
    IReadOnlyList<string> Styles,
    ShowStatus Status,
    string? FlyerThumbUrl,
    int AttendingCount,
    ProfileShowCityResponse City,
    string? FlyerId)
    : ShowSummaryResponse(Id, Slug, Title, StartsAt, VenueName, Lineup, Styles, Status, FlyerThumbUrl, AttendingCount)
{
    /// <summary>O resumo <paramref name="summary"/> com a cidade <paramref name="city"/> e o flyer <paramref name="flyerId"/>.</summary>
    public static ProfileShowResponse From(ShowSummaryResponse summary, ProfileShowCityResponse city, string? flyerId)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new ProfileShowResponse(
            summary.Id,
            summary.Slug,
            summary.Title,
            summary.StartsAt,
            summary.VenueName,
            summary.Lineup,
            summary.Styles,
            summary.Status,
            summary.FlyerThumbUrl,
            summary.AttendingCount,
            city,
            string.IsNullOrEmpty(flyerId) ? null : flyerId);
    }
}

/// <summary>SPEC-0026 — a cidade de um show da lista do perfil.</summary>
public sealed record ProfileShowCityResponse(string Slug, string Name, string TimeZone);
