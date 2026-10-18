using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.ChangeShowStatus;

/// <summary>
/// SPEC-0023 — <c>POST /api/v1/shows/{id}/status</c>: <paramref name="Status"/> = <c>scheduled</c> | <c>cancelled</c> |
/// <c>postponed</c>, pelo dono do organizador ou pelo admin.
/// </summary>
public sealed record ChangeShowStatusCommand(Guid ShowId, Guid UserId, bool IsAdmin, string? Status) : ICommand;
