using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;

/// <summary>
/// SPEC-0026 — <c>GET /api/v1/profiles/{type}/{slug}</c> (público): o perfil aprovado do tipo e slug, para as páginas
/// <c>/banda|casa|produtor/{slug}</c>; inexistente ou de outro tipo → <c>profiles.not_found</c>.
/// </summary>
public sealed record GetPublicProfileQuery(string Type, string Slug) : IQuery<PublicProfileResponse>;

/// <summary>SPEC-0026 — o perfil público (§6): sem dono, contato nem e-mail.</summary>
public sealed record PublicProfileResponse(
    Guid Id,
    string Type,
    string Name,
    string Slug,
    ProfileCityResponse City,
    IReadOnlyList<ProfileStyleResponse> Styles,
    PublicProfileLinksResponse Links);

/// <summary>
/// SPEC-0026 — os links das redes do perfil (§6: <c>{instagram?, bandcamp?, metalArchives?}</c>), só os validados no
/// cadastro; o não informado vem nulo.
/// </summary>
public sealed record PublicProfileLinksResponse
{
    public string? Instagram { get; init; }

    public string? Bandcamp { get; init; }

    public string? MetalArchives { get; init; }
}

/// <summary>SPEC-0026 — a cidade de um perfil: slug e nome.</summary>
public sealed record ProfileCityResponse(string Slug, string Name);

/// <summary>SPEC-0026 — um estilo de um perfil: slug e nome.</summary>
public sealed record ProfileStyleResponse(string Slug, string Name);

/// <summary>SPEC-0026 — os dados de um perfil aprovado lidos do schema <c>profiles</c> (cidade e estilos em slugs).</summary>
internal sealed record PublicProfileData(
    Guid Id,
    string Type,
    string Name,
    string Slug,
    string City,
    IReadOnlyList<string> Styles,
    string? Instagram,
    string? Bandcamp,
    string? MetalArchives);
