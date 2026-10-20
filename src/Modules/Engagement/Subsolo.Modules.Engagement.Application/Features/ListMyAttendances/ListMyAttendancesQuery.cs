using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyAttendances;

/// <summary>
/// SPEC-0029 — <c>GET /api/v1/me/attendances?period=upcoming</c>: os shows em que o fã <paramref name="UserId"/> marcou
/// "vou" e que ainda não começaram (<paramref name="Period"/>: só <c>upcoming</c>, o padrão).
/// </summary>
public sealed record ListMyAttendancesQuery(Guid UserId, string? Period) : IQuery<MyAttendancesResponse>;

/// <summary>SPEC-0029 — os ids dos shows (§6), do "vou" mais antigo para o mais novo.</summary>
public sealed record MyAttendancesResponse(IReadOnlyList<Guid> ShowIds);
