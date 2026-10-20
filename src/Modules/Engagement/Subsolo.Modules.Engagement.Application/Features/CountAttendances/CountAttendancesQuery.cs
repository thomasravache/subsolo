using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Engagement.Application.Features.CountAttendances;

/// <summary>
/// SPEC-0029 — <c>GET /api/v1/attendances/counts?showIds=a,b,c</c> (público): quantos marcaram "vou" em cada show, com
/// os ids separados por vírgula (até <see cref="CountAttendancesQueryHandler.MaxShows"/>).
/// </summary>
public sealed record CountAttendancesQuery(string? ShowIds) : IQuery<AttendanceCountsResponse>;

/// <summary>SPEC-0029 — só números por show (ADR-0008: nunca quem vai); cada show pedido aparece, 0 se ninguém marcou.</summary>
public sealed record AttendanceCountsResponse(IReadOnlyDictionary<Guid, int> Counts);
