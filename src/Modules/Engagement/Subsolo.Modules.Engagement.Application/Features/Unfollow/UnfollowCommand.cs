using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Application.Features.Unfollow;

/// <summary>
/// SPEC-0027 — <c>DELETE /api/v1/me/follows/cities/{slug}</c> e <c>DELETE /api/v1/me/follows/profiles/{profileId}</c>:
/// o fã <paramref name="UserId"/> deixa de seguir <paramref name="Target"/> (idempotente: sem follow, nada muda).
/// </summary>
public sealed record UnfollowCommand(Guid UserId, FollowTarget Target) : ICommand;
