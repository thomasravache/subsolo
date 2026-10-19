namespace Subsolo.Modules.Events.Domain.Flyers;

/// <summary>SPEC-0022 — flyers no schema <c>events</c>.</summary>
public interface IFlyerRepository
{
    /// <summary>Rastreado: um <see cref="Flyer.Attach"/> é gravado no próximo <c>SaveChangesAsync</c>.</summary>
    Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// SPEC-0023 (H22-01) — apaga, num comando só e com a condição no banco (<c>attached_at_utc IS NULL AND
    /// created_at_utc &lt; corte</c>), as linhas dos flyers não ligados criados antes de <paramref name="cutoffUtc"/>, e
    /// devolve os ids apagados (só destes a limpeza apaga os arquivos, depois).
    /// </summary>
    Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);

    void Add(Flyer flyer);

    void Remove(Flyer flyer);
}
