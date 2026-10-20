using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.UnattendShow;

/// <summary>
/// SPEC-0029 — <c>DELETE /api/v1/me/attendances/{showId}</c>: o fã <paramref name="UserId"/> desmarca o "vou" no show
/// <paramref name="ShowId"/> (idempotente: sem "vou", nada muda).
/// </summary>
public sealed record UnattendShowCommand(Guid UserId, Guid ShowId) : ICommand;
