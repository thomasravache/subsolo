namespace Subsolo.Modules.Moderation.Application;

/// <summary>
/// SPEC-0039 (H37-02) — trava por show entre a denúncia (<c>ReportShow</c>) e a decisão de ocultar (<c>HideShow</c>): as
/// leituras e a gravação de cada uma acontecem inteiras sob a trava (no PostgreSQL, <c>pg_advisory_xact_lock</c> numa
/// transação), e a denúncia que chega durante a decisão espera o commit dela e já vê o show oculto.
/// </summary>
internal interface IShowLocks
{
    /// <summary>Espera e toma a trava de <paramref name="showId"/>; as leituras e as gravações da unidade de trabalho entram na transação dela.</summary>
    Task<IShowLock> AcquireAsync(Guid showId, CancellationToken cancellationToken);
}

/// <summary>A trava tomada: <see cref="CommitAsync"/> confirma o que foi gravado e a solta; descartada sem isso, desfaz e solta.</summary>
internal interface IShowLock : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
