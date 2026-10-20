using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Engagement.Application;

namespace Subsolo.Modules.Engagement.Infrastructure.Attendances;

/// <summary>SPEC-0029 — remoções de "vou" direto no banco (desmarcar e exclusão da conta).</summary>
internal sealed class AttendancesMaintenance(EngagementDbContext db) : IAttendancesMaintenance
{
    public Task<int> DeleteAsync(Guid userId, Guid showId, CancellationToken cancellationToken) =>
        db.Attendances
            .Where(attendance => attendance.UserId == userId && attendance.ShowId == showId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAllOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Attendances.Where(attendance => attendance.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
