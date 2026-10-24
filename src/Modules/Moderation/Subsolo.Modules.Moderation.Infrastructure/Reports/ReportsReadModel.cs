using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Features.ListReports;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Infrastructure.Reports;

/// <summary>
/// SPEC-0033 — leituras sem rastreamento de <c>moderation.reports</c> (a fila, só as colunas que ela mostra: nunca quem
/// denunciou) e a gravação em lote da exclusão da conta.
/// </summary>
internal sealed class ReportsReadModel(ModerationDbContext db) : IReportsReadModel
{
    public async Task<IReadOnlyList<PendingReport>> ListPendingAsync(CancellationToken cancellationToken) =>
        await db.Reports.AsNoTracking()
            .Where(report => report.Status == ReportStatuses.Pending)
            .Select(report => new PendingReport(report.ShowId, report.Reason, report.Details, report.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public Task<int> ForgetReporterAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Reports
            .Where(report => report.ReporterUserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(report => report.ReporterUserId, (Guid?)null), cancellationToken);
}
