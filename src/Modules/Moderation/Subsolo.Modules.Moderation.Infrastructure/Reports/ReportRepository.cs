using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Infrastructure.Reports;

/// <summary>SPEC-0033 — denúncias no schema <c>moderation</c> (rastreadas: a decisão muda a situação).</summary>
internal sealed class ReportRepository(ModerationDbContext db) : IReportRepository
{
    public Task<bool> HasPendingAsync(Guid showId, Guid reporterUserId, CancellationToken cancellationToken) =>
        db.Reports.AnyAsync(
            report => report.ShowId == showId
                && report.ReporterUserId == reporterUserId
                && report.Status == ReportStatuses.Pending,
            cancellationToken);

    public async Task<IReadOnlyList<Report>> ListPendingOfShowAsync(Guid showId, CancellationToken cancellationToken) =>
        await db.Reports
            .Where(report => report.ShowId == showId && report.Status == ReportStatuses.Pending)
            .OrderBy(report => report.CreatedAtUtc)
            .ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

    public void Add(Report report) => db.Reports.Add(report);
}
