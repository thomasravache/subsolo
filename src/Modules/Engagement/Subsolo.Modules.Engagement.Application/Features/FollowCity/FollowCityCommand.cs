using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.FollowCity;

/// <summary>SPEC-0027 — <c>PUT /api/v1/me/follows/cities/{slug}</c>: o fã <paramref name="UserId"/> segue a cidade (idempotente).</summary>
public sealed record FollowCityCommand(Guid UserId, string CitySlug) : ICommand;
