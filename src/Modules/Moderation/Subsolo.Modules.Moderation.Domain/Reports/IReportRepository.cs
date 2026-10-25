namespace Subsolo.Modules.Moderation.Domain.Reports;

/// <summary>SPEC-0033 — denúncias no schema <c>moderation</c> (rastreadas: a decisão muda a situação).</summary>
public interface IReportRepository
{
    /// <summary>O fã <paramref name="reporterUserId"/> já tem denúncia pendente do show <paramref name="showId"/>.</summary>
    Task<bool> HasPendingAsync(Guid showId, Guid reporterUserId, CancellationToken cancellationToken);

    /// <summary>
    /// SPEC-0037 (H33-05) — o Moderation já decidiu ocultar o show (há denúncia <c>hidden</c> dele), mesmo que o Events
    /// ainda não tenha consumido a decisão.
    /// </summary>
    Task<bool> IsHiddenAsync(Guid showId, CancellationToken cancellationToken);

    /// <summary>As denúncias pendentes do show, das mais antigas para as mais novas.</summary>
    Task<IReadOnlyList<Report>> ListPendingOfShowAsync(Guid showId, CancellationToken cancellationToken);

    void Add(Report report);
}
