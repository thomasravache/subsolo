using Subsolo.Common.Domain;

namespace Subsolo.Modules.Moderation.Domain.Reports;

/// <summary>
/// SPEC-0033 — denúncia de um show por um fã: motivo (<see cref="ReportReasons"/>), detalhe opcional (até 500
/// caracteres), situação (<see cref="ReportStatuses"/>) e a decisão do admin (quando, quem e a nota interna de quem
/// ocultou). Quem denunciou (<see cref="ReporterUserId"/>) fica nulo quando a conta é excluída: o histórico de moderação
/// continua (ADR-0008).
/// </summary>
public sealed class Report : Entity
{
    /// <summary>Tamanho máximo da nota interna da decisão de ocultar.</summary>
    public const int DecisionNoteMaxLength = 200;

    private Report()
    {
    }

    public Guid Id { get; private set; }

    public Guid ShowId { get; private set; }

    /// <summary>O fã que denunciou; <see langword="null"/> depois da exclusão da conta.</summary>
    public Guid? ReporterUserId { get; private set; }

    /// <summary><see cref="ReportReasons"/>.</summary>
    public string Reason { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    /// <summary><see cref="ReportStatuses"/>.</summary>
    public string Status { get; private set; } = ReportStatuses.Pending;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    /// <summary>O admin que decidiu.</summary>
    public Guid? DecidedBy { get; private set; }

    /// <summary>A nota interna de quem ocultou (nunca sai pela API nem pelo export).</summary>
    public string? DecisionNote { get; private set; }

    public bool IsPending => Status == ReportStatuses.Pending;

    /// <summary>Denúncia nova, pendente. Quem chama já validou o motivo e o detalhe.</summary>
    public static Report Submit(Guid showId, Guid reporterUserId, string reason, string? details, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!ReportReasons.All.Contains(reason, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Motivo de denúncia desconhecido: {reason}.", nameof(reason));
        }

        var trimmed = details?.Trim();
        return new Report
        {
            Id = Guid.CreateVersion7(),
            ShowId = showId,
            ReporterUserId = reporterUserId,
            Reason = reason,
            Details = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            Status = ReportStatuses.Pending,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// O admin <paramref name="adminId"/> manteve o show: todas as denúncias pendentes dele (<paramref name="pending"/>)
    /// viram <see cref="ReportStatuses.Kept"/>.
    /// </summary>
    public static void KeepShow(IReadOnlyList<Report> pending, Guid adminId, DateTimeOffset nowUtc) =>
        Decide(pending, ReportStatuses.Kept, adminId, note: null, nowUtc);

    /// <summary>
    /// O admin <paramref name="adminId"/> ocultou o show: todas as denúncias pendentes dele (<paramref name="pending"/>)
    /// viram <see cref="ReportStatuses.Hidden"/>, com a nota interna, e levanta
    /// <see cref="ShowHiddenByModerationDomainEvent"/> uma vez.
    /// </summary>
    public static void HideShow(IReadOnlyList<Report> pending, Guid adminId, string? note, DateTimeOffset nowUtc)
    {
        Decide(pending, ReportStatuses.Hidden, adminId, note, nowUtc);
        pending[0].Raise(new ShowHiddenByModerationDomainEvent(pending[0].ShowId, nowUtc));
    }

    private static void Decide(IReadOnlyList<Report> pending, string status, Guid adminId, string? note, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (pending.Count == 0)
        {
            throw new ArgumentException("Nenhuma denúncia pendente para decidir.", nameof(pending));
        }

        var showId = pending[0].ShowId;
        var trimmed = note?.Trim();
        foreach (var report in pending)
        {
            if (!report.IsPending || report.ShowId != showId)
            {
                throw new InvalidOperationException("Só denúncias pendentes do mesmo show são decididas juntas.");
            }

            report.Status = status;
            report.DecidedAtUtc = nowUtc;
            report.DecidedBy = adminId;
            report.DecisionNote = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
