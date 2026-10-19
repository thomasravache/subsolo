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

/// <summary>SPEC-0026 — corpo de <c>GET /api/v1/shows?profileId=&amp;period=</c>.</summary>
public sealed record ProfileShowsResponse(IReadOnlyList<ShowSummaryResponse> Shows);
