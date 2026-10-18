namespace Subsolo.Modules.Events.Domain.Flyers;

/// <summary>SPEC-0022 — flyers no schema <c>events</c>.</summary>
public interface IFlyerRepository
{
    /// <summary>Rastreado: um <see cref="Flyer.Attach"/> é gravado no próximo <c>SaveChangesAsync</c>.</summary>
    Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);

    /// <summary>Os não ligados a show criados antes de <paramref name="cutoffUtc"/>.</summary>
    Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);

    void Add(Flyer flyer);

    void Remove(Flyer flyer);
}
