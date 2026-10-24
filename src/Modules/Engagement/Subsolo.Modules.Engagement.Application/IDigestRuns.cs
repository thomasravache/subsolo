namespace Subsolo.Modules.Engagement.Application;

/// <summary>
/// SPEC-0032 (H31-01) — os dias com resumo diário concluído (§6: <c>engagement.digest_runs(digest_date date PK,
/// completed_at_utc timestamptz)</c>, uma linha por dia de São Paulo); o gatilho de recuperação lê daqui se o resumo do dia
/// já saiu. Sem dados de usuário.
/// </summary>
internal interface IDigestRuns
{
    /// <summary>Há resumo concluído no dia <paramref name="digestDate"/> (São Paulo).</summary>
    Task<bool> CompletedAsync(DateOnly digestDate, CancellationToken cancellationToken);

    /// <summary>Grava (ou atualiza) a conclusão do resumo do dia <paramref name="digestDate"/>.</summary>
    Task RecordAsync(DateOnly digestDate, DateTimeOffset completedAtUtc, CancellationToken cancellationToken);
}
