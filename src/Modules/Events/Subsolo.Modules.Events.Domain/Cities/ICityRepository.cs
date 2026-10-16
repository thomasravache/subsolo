namespace Subsolo.Modules.Events.Domain.Cities;

/// <summary>SPEC-0008 — leitura de cidades pelo slug; SPEC-0011 — lista de todas as cidades.</summary>
public interface ICityRepository
{
    /// <summary>A cidade com o <paramref name="slug"/>, ou <see langword="null"/> se não está cadastrada.</summary>
    Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Todas as cidades cadastradas, ordenadas pelo nome (depois pelo slug, para empates).</summary>
    Task<IReadOnlyList<City>> ListAsync(CancellationToken cancellationToken);
}
