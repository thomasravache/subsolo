namespace Subsolo.Modules.Events.Domain.Styles;

/// <summary>SPEC-0017 — estilos no schema <c>events</c>.</summary>
public interface IStyleRepository
{
    /// <summary>O estilo com o <paramref name="slug"/>, ou <see langword="null"/>.</summary>
    Task<Style?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Todos os estilos, ordenados pelo nome.</summary>
    Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Inclui um estilo novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Style style);
}
