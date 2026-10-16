namespace Subsolo.Modules.Events.Domain.Cities;

/// <summary>SPEC-0008 — leitura de cidades pelo slug.</summary>
public interface ICityRepository
{
    /// <summary>A cidade com o <paramref name="slug"/>, ou <see langword="null"/> se não está cadastrada.</summary>
    Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
}
