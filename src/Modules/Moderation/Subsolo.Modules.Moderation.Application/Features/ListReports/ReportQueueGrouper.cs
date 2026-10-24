using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.ListReports;

/// <summary>SPEC-0033 — uma denúncia pendente, como o agrupador da fila a lê (sem quem denunciou).</summary>
internal sealed record PendingReport(Guid ShowId, string Reason, string? Details, DateTimeOffset CreatedAtUtc);

/// <summary>SPEC-0033 — a quantidade de denúncias pendentes de um show por motivo.</summary>
internal sealed record ReportCounts(int Fake, int CancelledUnannounced, int WrongInfo, int Other);

/// <summary>
/// SPEC-0033 — um show na fila: as contagens por motivo, até três detalhes mais recentes (os não vazios, do mais novo
/// para o mais antigo) e o instante da primeira denúncia pendente.
/// </summary>
internal sealed record ReportedShowGroup(
    Guid ShowId,
    ReportCounts Counts,
    IReadOnlyList<string> LatestDetails,
    DateTimeOffset FirstReportedAtUtc);

/// <summary>
/// SPEC-0033 (UT-02) — o agrupador da fila: um item por show, pela ordem da primeira denúncia (mais antigos primeiro;
/// empate pelo id do show), com as contagens por motivo e até <see cref="LatestDetailsLimit"/> detalhes recentes.
/// </summary>
internal static class ReportQueueGrouper
{
    /// <summary>Até quantos detalhes recentes cada item leva (§6).</summary>
    public const int LatestDetailsLimit = 3;

    public static IReadOnlyList<ReportedShowGroup> Group(IEnumerable<PendingReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        return
        [
            .. reports
                .GroupBy(report => report.ShowId)
                .Select(Summarize)
                .OrderBy(group => group.FirstReportedAtUtc)
                .ThenBy(group => group.ShowId),
        ];
    }

    private static ReportedShowGroup Summarize(IGrouping<Guid, PendingReport> show)
    {
        int Count(string reason) => show.Count(report => report.Reason == reason);

        return new ReportedShowGroup(
            show.Key,
            new ReportCounts(
                Count(ReportReasons.Fake),
                Count(ReportReasons.CancelledUnannounced),
                Count(ReportReasons.WrongInfo),
                Count(ReportReasons.Other)),
            [
                .. show
                    .Where(report => !string.IsNullOrWhiteSpace(report.Details))
                    .OrderByDescending(report => report.CreatedAtUtc)
                    .Take(LatestDetailsLimit)
                    .Select(report => report.Details!),
            ],
            show.Min(report => report.CreatedAtUtc));
    }
}
