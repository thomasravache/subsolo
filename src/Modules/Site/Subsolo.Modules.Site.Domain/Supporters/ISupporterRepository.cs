namespace Subsolo.Modules.Site.Domain.Supporters;

/// <summary>SPEC-0028 — apoiadores no schema <c>site</c>.</summary>
public interface ISupporterRepository
{
    /// <summary>Todos os apoiadores, na ordem de cadastro.</summary>
    Task<IReadOnlyList<Supporter>> ListAsync(CancellationToken cancellationToken);

    /// <summary>O apoiador com o id, ou <see langword="null"/>.</summary>
    Task<Supporter?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Inclui um apoiador novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Supporter supporter);

    /// <summary>Remove o apoiador (no próximo <c>SaveChangesAsync</c>).</summary>
    void Remove(Supporter supporter);
}
