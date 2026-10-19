using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;

namespace Subsolo.Modules.Profiles.Application.Features.LookupProfiles;

/// <summary>
/// SPEC-0026 (H23-08, Emenda v2 a) — <c>GET /api/v1/profiles/lookup?q=&amp;type=</c> (artista ou admin): os perfis
/// aprovados, com ou sem dono, cujo nome contém <see cref="Q"/> (ao menos 2 caracteres), do <see cref="Type"/> quando
/// informado; até 20, por nome. É a busca do cadastro de show (a casa e, para o admin, o organizador).
/// </summary>
public sealed record LookupProfilesQuery(string? Q, string? Type) : IQuery<IReadOnlyList<ProfileLookupResponse>>;

/// <summary>SPEC-0026 — um perfil na busca do cadastro de show (sem dono nem contato).</summary>
public sealed record ProfileLookupResponse(Guid Id, string Type, string Name, string Slug, ProfileCityResponse City);

/// <summary>SPEC-0026 — um perfil da busca lido do schema <c>profiles</c> (a cidade em slug).</summary>
internal sealed record ProfileLookupData(Guid Id, string Type, string Name, string Slug, string City);
