namespace Subsolo.Modules.Events.Application;

/// <summary>
/// SPEC-0026 (H24-02) — trava por id de flyer entre a limpeza e um envio simultâneo do mesmo arquivo: a conferência e a
/// exclusão dos arquivos pela limpeza, e a gravação dos arquivos e da linha pelo envio, acontecem cada uma inteira sob a
/// trava (no PostgreSQL, <c>pg_advisory_xact_lock</c> numa transação), e uma nunca corta a outra ao meio.
/// </summary>
internal interface IFlyerLocks
{
    /// <summary>Espera e toma a trava de <paramref name="flyerId"/>; as gravações da unidade de trabalho entram na transação dela.</summary>
    Task<IFlyerLock> AcquireAsync(string flyerId, CancellationToken cancellationToken);
}

/// <summary>A trava tomada: <see cref="CommitAsync"/> confirma o que foi gravado e a solta; descartada sem isso, desfaz e solta.</summary>
internal interface IFlyerLock : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
