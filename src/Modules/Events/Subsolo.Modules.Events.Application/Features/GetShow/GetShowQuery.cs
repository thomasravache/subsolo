using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Application.Features.GetShow;

/// <summary>
/// SPEC-0023 — <c>GET /api/v1/shows/{id}</c>: o show como a tela "Editar show" precisa (o início em hora local da
/// cidade), para o dono do organizador ou o admin.
/// </summary>
public sealed record GetShowQuery(Guid ShowId, Guid UserId, bool IsAdmin) : IQuery<ShowForEditResponse>;

/// <summary>SPEC-0023 — casa ou banda no formulário: com perfil (<paramref name="ProfileId"/>) ou só o nome.</summary>
public sealed record ShowPartResponse(Guid? ProfileId, string Name);

/// <summary>SPEC-0023 — corpo de <c>GET /api/v1/shows/{id}</c>: os campos do cadastro, a situação e o flyer.</summary>
public sealed record ShowForEditResponse(
    Guid Id,
    string Slug,
    Guid OrganizerProfileId,
    string OrganizerName,
    string Title,
    string City,
    string StartsLocal,
    ShowPartResponse Venue,
    IReadOnlyList<ShowPartResponse> Lineup,
    IReadOnlyList<string> Styles,
    string Price,
    string? TicketUrl,
    string FlyerId,
    string FlyerThumbUrl,
    ShowStatus Status);

/// <summary>SPEC-0023 — o show como gravado (leitura interna da edição).</summary>
internal sealed record ShowDetails(
    Guid Id,
    string Slug,
    Guid OrganizerProfileId,
    string Title,
    string City,
    DateTimeOffset StartsAtUtc,
    ShowPartResponse Venue,
    IReadOnlyList<ShowPartResponse> Lineup,
    IReadOnlyList<string> Styles,
    string Price,
    string? TicketUrl,
    string FlyerId,
    string Status);
