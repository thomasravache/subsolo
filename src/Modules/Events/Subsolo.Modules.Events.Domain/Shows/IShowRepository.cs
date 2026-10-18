namespace Subsolo.Modules.Events.Domain.Shows;

/// <summary>SPEC-0023 — shows no schema <c>events</c>.</summary>
public interface IShowRepository
{
    /// <summary>Rastreado: uma edição é gravada no próximo <c>SaveChangesAsync</c>.</summary>
    Task<Show?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Já existe show com <paramref name="slug"/> na cidade <paramref name="city"/>.</summary>
    Task<bool> SlugExistsAsync(string city, string slug, CancellationToken cancellationToken);

    void Add(Show show);
}
