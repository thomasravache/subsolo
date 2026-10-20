using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.AttendShow;

/// <summary>SPEC-0029 — <c>PUT /api/v1/me/attendances/{showId}</c>: o fã <paramref name="UserId"/> marca "vou" (idempotente).</summary>
public sealed record AttendShowCommand(Guid UserId, Guid ShowId) : ICommand;
