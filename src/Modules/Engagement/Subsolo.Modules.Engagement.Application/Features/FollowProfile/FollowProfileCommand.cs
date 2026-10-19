using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.FollowProfile;

/// <summary>SPEC-0027 — <c>PUT /api/v1/me/follows/profiles/{profileId}</c>: o fã <paramref name="UserId"/> segue o perfil (idempotente).</summary>
public sealed record FollowProfileCommand(Guid UserId, Guid ProfileId) : ICommand;
