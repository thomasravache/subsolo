using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.ListMyProfiles;

/// <summary>SPEC-0021 — <c>GET /api/v1/me/profiles</c>: os perfis do fã <paramref name="UserId"/> e a situação dos pedidos dele.</summary>
public sealed record ListMyProfilesQuery(Guid UserId) : IQuery<MyProfilesResponse>;

/// <summary>SPEC-0021 — "Meus perfis": os perfis de que o usuário é dono e os pedidos dele (sem contato nem prova).</summary>
public sealed record MyProfilesResponse(IReadOnlyList<MyProfileResponse> Profiles, IReadOnlyList<MyProfileRequestResponse> Requests);

/// <summary>SPEC-0021 — um perfil do usuário.</summary>
public sealed record MyProfileResponse(Guid Id, string Type, string Name, string Slug, string City);

/// <summary>
/// SPEC-0021 — um pedido do usuário: tipo do pedido (<c>new</c> | <c>claim</c>), nome e tipo do perfil, situação
/// (<c>pending</c> | <c>approved</c> | <c>rejected</c>) e o motivo da recusa.
/// </summary>
public sealed record MyProfileRequestResponse(
    Guid RequestId,
    string Kind,
    string Name,
    string Type,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAtUtc);
