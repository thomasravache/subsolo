using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Attendances;

namespace Subsolo.Modules.Engagement.Infrastructure.Attendances;

/// <summary>SPEC-0029 — leituras dos "vou" do fã (sem rastreamento).</summary>
internal sealed class AttendancesReadModel(EngagementDbContext db) : IAttendancesReadModel
{
    public async Task<IReadOnlyList<Attendance>> ListOfAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Attendances.AsNoTracking()
            .Where(attendance => attendance.UserId == userId)
            .OrderBy(attendance => attendance.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}
