namespace Subsolo.Modules.Events.IntegrationEvents;

/// <summary>
/// SPEC-0026 — os nomes das cidades e dos estilos do Events para outros módulos (ADR-0001: interface pública em
/// IntegrationEvents, nunca o banco do Events). O módulo Profiles mostra com ela a cidade e os estilos do perfil público e
/// da busca de perfis.
/// </summary>
public interface IEventsCatalogNames
{
    /// <summary>
    /// Os nomes, por slug, das cidades <paramref name="citySlugs"/> e dos estilos <paramref name="styleSlugs"/> que existem
    /// (os inexistentes ficam de fora).
    /// </summary>
    Task<CatalogNames> GetNamesAsync(
        IReadOnlyCollection<string> citySlugs,
        IReadOnlyCollection<string> styleSlugs,
        CancellationToken ct);
}

/// <summary>SPEC-0026 — nomes de cidades e estilos por slug.</summary>
public sealed record CatalogNames(IReadOnlyDictionary<string, string> Cities, IReadOnlyDictionary<string, string> Styles);
