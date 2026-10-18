namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>SPEC-0021 — perfis no schema <c>profiles</c> (escrita: pedido, aprovação, criação pelo admin).</summary>
public interface IProfileRepository
{
    /// <summary>O perfil <paramref name="id"/>, ou <see langword="null"/>.</summary>
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Já existe perfil do <paramref name="type"/> com o <paramref name="slug"/>.</summary>
    Task<bool> ExistsAsync(string type, string slug, CancellationToken cancellationToken);

    /// <summary>Inclui um perfil novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Profile profile);
}
