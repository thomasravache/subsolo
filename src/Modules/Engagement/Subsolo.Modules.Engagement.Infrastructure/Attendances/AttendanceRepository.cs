using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Domain.Attendances;

namespace Subsolo.Modules.Engagement.Infrastructure.Attendances;

/// <summary>SPEC-0029 — "vou" no schema <c>engagement</c>.</summary>
internal sealed class AttendanceRepository(EngagementDbContext db) : IAttendanceRepository
{
    public Task<bool> ExistsAsync(Guid userId, Guid showId, CancellationToken cancellationToken) =>
        db.Attendances.AnyAsync(attendance => attendance.UserId == userId && attendance.ShowId == showId, cancellationToken);

    public void Add(Attendance attendance) => db.Attendances.Add(attendance);

    /// <summary>Uma consulta pelo índice do show: <c>show_id = any(@ids) group by show_id</c>.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> CountAsync(
        IReadOnlyCollection<Guid> showIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        var ids = showIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await db.Attendances.AsNoTracking()
            .Where(attendance => ids.Contains(attendance.ShowId))
            .GroupBy(attendance => attendance.ShowId)
            .Select(group => new { ShowId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ShowId, row => row.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetAttendeeIdsAsync(Guid showId, CancellationToken cancellationToken) =>
        await db.Attendances.AsNoTracking()
            .Where(attendance => attendance.ShowId == showId)
            .Select(attendance => attendance.UserId)
            .ToListAsync(cancellationToken);
}
