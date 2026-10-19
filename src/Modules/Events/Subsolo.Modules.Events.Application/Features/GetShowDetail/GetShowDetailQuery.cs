using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;

namespace Subsolo.Modules.Events.Application.Features.GetShowDetail;

/// <summary>SPEC-0024 — <c>GET /api/v1/cities/{city}/shows/{slug}</c> (público): a página do show.</summary>
public sealed record GetShowDetailQuery(string City, string Slug) : IQuery<ShowDetailResponse>;

/// <summary>
/// SPEC-0024 — corpo do detalhe público do show (§6): sem nada de quem cadastrou. <see cref="StartsAt"/> em UTC
/// (serializado com <c>Z</c>); a casa e as bandas com o slug do perfil quando têm perfil aprovado.
/// </summary>
public sealed record ShowDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    ShowDetailCityResponse City,
    DateTime StartsAt,
    ShowDetailPartResponse Venue,
    IReadOnlyList<ShowDetailPartResponse> Lineup,
    IReadOnlyList<ShowDetailStyleResponse> Styles,
    string Price,
    string? TicketUrl,
    ShowStatus Status,
    ShowDetailFlyerResponse Flyer);

/// <summary>SPEC-0024 — a cidade do show: slug, nome e fuso (IANA).</summary>
public sealed record ShowDetailCityResponse(string Slug, string Name, string TimeZone);

/// <summary>SPEC-0024 — a casa ou uma banda: o nome e o slug do perfil (<see langword="null"/> = sem perfil).</summary>
public sealed record ShowDetailPartResponse(string Name, string? ProfileSlug);

/// <summary>SPEC-0024 — um estilo do show: slug e nome.</summary>
public sealed record ShowDetailStyleResponse(string Slug, string Name);

/// <summary>
/// SPEC-0024 — as URLs das variantes do flyer e as medidas da <c>full</c> (Emenda v2, b: <see langword="null"/> nos
/// flyers enviados antes da medida).
/// </summary>
public sealed record ShowDetailFlyerResponse(string ThumbUrl, string FullUrl, string OgUrl, int? Width, int? Height);

/// <summary>SPEC-0024 — o show como gravado e as medidas da variante <c>full</c> do flyer (leitura interna).</summary>
internal sealed record PublicShowDetails(ShowDetails Show, int? FlyerWidth, int? FlyerHeight);
