using System.Globalization;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Domain.Attendances;

namespace Subsolo.Modules.Engagement.Application.Features.CountAttendances;

/// <summary>
/// SPEC-0029 — lê os ids (separados por vírgula, sem repetição) e conta numa consulta só; mais de
/// <see cref="MaxShows"/> ids ou um id inválido → <see cref="EngagementErrors.InvalidQuery"/>; sem ids → contagem vazia.
/// </summary>
internal sealed class CountAttendancesQueryHandler(IAttendanceRepository attendances)
    : IQueryHandler<CountAttendancesQuery, AttendanceCountsResponse>
{
    /// <summary>Máximo de shows por consulta.</summary>
    public const int MaxShows = 100;

    public async Task<Result<AttendanceCountsResponse>> Handle(CountAttendancesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parts = (query.ShowIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > MaxShows)
        {
            return Invalid($"No máximo {MaxShows} shows por consulta.");
        }

        var showIds = new HashSet<Guid>();
        foreach (var part in parts)
        {
            if (!Guid.TryParseExact(part, "D", out var id))
            {
                return Invalid("Ids de show inválidos.");
            }

            showIds.Add(id);
        }

        var counts = showIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await attendances.CountAsync(showIds, cancellationToken);
        return Result.Success(new AttendanceCountsResponse(
            showIds.ToDictionary(id => id, id => counts.GetValueOrDefault(id))));
    }

    private static Result<AttendanceCountsResponse> Invalid(string message) =>
        Result.Failure<AttendanceCountsResponse>(EngagementErrors.InvalidQueryError("showIds", message));
}
