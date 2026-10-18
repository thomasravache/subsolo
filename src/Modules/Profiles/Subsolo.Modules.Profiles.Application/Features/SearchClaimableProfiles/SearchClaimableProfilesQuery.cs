using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.SearchClaimableProfiles;

/// <summary>
/// SPEC-0021 — <c>GET /api/v1/profiles/search?q=&amp;type=</c>: perfis aprovados sem dono que o fã pode reivindicar
/// (q com ao menos 2 caracteres; até 20 resultados).
/// </summary>
public sealed record SearchClaimableProfilesQuery(string? Q, string? Type) : IQuery<IReadOnlyList<ClaimableProfileResponse>>;

/// <summary>SPEC-0021 — um perfil sem dono na busca de reivindicação (sem contato nem dono).</summary>
public sealed record ClaimableProfileResponse(Guid Id, string Type, string Name, string City, IReadOnlyList<string> Styles);
