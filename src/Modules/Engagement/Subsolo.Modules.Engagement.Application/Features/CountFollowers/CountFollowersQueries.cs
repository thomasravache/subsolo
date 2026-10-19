using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.CountFollowers;

/// <summary>SPEC-0027 — <c>GET /api/v1/cities/{slug}/followers</c>: quantos seguem a cidade (público).</summary>
public sealed record CountCityFollowersQuery(string CitySlug) : IQuery<FollowerCountResponse>;

/// <summary>SPEC-0027 — <c>GET /api/v1/profiles/{profileId}/followers</c>: quantos seguem o perfil (público).</summary>
public sealed record CountProfileFollowersQuery(Guid ProfileId) : IQuery<FollowerCountResponse>;

/// <summary>SPEC-0027 — a contagem pública: só o número, sem ids nem nomes de quem segue.</summary>
public sealed record FollowerCountResponse(int Count);
