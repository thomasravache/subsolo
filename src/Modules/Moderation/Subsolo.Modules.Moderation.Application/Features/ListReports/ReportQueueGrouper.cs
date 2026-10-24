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
/// SPEC-0033 — scaffold do agrupador da fila (UT-02): um item por show, pela ordem da primeira denúncia (mais antigos
/// primeiro). Implementação pendente.
/// </summary>
internal static class ReportQueueGrouper
{
    /// <summary>Até quantos detalhes recentes cada item leva (§6).</summary>
    public const int LatestDetailsLimit = 3;

    public static IReadOnlyList<ReportedShowGroup> Group(IEnumerable<PendingReport> reports) =>
        throw new NotImplementedException("SPEC-0033: agrupador da fila de denúncias");
}
