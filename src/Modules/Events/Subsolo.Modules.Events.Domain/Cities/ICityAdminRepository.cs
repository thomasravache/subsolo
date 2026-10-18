namespace Subsolo.Modules.Events.Domain.Cities;

/// <summary>SPEC-0017 — cadastro de cidades pelo painel do admin (as leituras públicas ficam em <see cref="ICityRepository"/>).</summary>
public interface ICityAdminRepository
{
    /// <summary>Já existe cidade com o <paramref name="slug"/>?</summary>
    Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken);

    /// <summary>A cidade com o <paramref name="slug"/>, para alterar (gravada no próximo <c>SaveChangesAsync</c>), ou <see langword="null"/>.</summary>
    Task<City?> GetForUpdateAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Inclui uma cidade nova (gravada no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(City city);
}
