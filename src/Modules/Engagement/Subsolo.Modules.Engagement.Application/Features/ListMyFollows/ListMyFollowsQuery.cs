using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyFollows;

/// <summary>SPEC-0027 — <c>GET /api/v1/me/follows</c>: o que o fã <paramref name="UserId"/> segue.</summary>
public sealed record ListMyFollowsQuery(Guid UserId) : IQuery<MyFollowsResponse>;

/// <summary>SPEC-0027 — as cidades e os perfis seguidos, com nome e endereço (§6).</summary>
public sealed record MyFollowsResponse(IReadOnlyList<FollowedCityResponse> Cities, IReadOnlyList<FollowedProfileResponse> Profiles);

/// <summary>Uma cidade seguida: slug e nome.</summary>
public sealed record FollowedCityResponse(string Slug, string Name);

/// <summary>Um perfil seguido: id, tipo (<c>band</c> | <c>venue</c> | <c>producer</c>), nome e slug.</summary>
public sealed record FollowedProfileResponse(Guid Id, string Type, string Name, string Slug);
