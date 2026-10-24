using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Moderation.Infrastructure;

/// <summary>
/// SPEC-0033 (ExportReports) — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019): as denúncias
/// do próprio titular, cada uma com <c>{ showId, reason, details, status, createdAtUtc }</c> (Emenda 1), da mais antiga
/// para a mais nova; nunca a nota interna nem quem decidiu. Sem denúncias, a seção é omitida.
/// </summary>
internal sealed class ModerationPersonalDataSection(ModerationDbContext db) : IPersonalDataSection
{
    public const string SectionName = "reports";

    public string Name => SectionName;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var reports = await db.Reports.AsNoTracking()
            .Where(report => report.ReporterUserId == userId)
            .OrderBy(report => report.CreatedAtUtc)
            .ThenBy(report => report.Id)
            .Select(report => new ExportedReport(report.ShowId, report.Reason, report.Details, report.Status, report.CreatedAtUtc))
            .ToListAsync(ct);
        return reports.Count == 0 ? null : reports;
    }

    private sealed record ExportedReport(Guid ShowId, string Reason, string? Details, string Status, DateTimeOffset CreatedAtUtc);
}
