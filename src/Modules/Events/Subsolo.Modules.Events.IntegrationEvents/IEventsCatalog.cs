namespace Subsolo.Modules.Events.IntegrationEvents;

/// <summary>
/// SPEC-0021 — leitura das cidades e estilos do Events por outros módulos (ADR-0001: interface pública em
/// IntegrationEvents, nunca o banco do Events). O módulo Profiles valida a cidade e os estilos de um perfil com ela.
/// </summary>
public interface IEventsCatalog
{
    /// <summary>Existe cidade com o slug <paramref name="slug"/>.</summary>
    Task<bool> CityExistsAsync(string slug, CancellationToken ct);

    /// <summary>Todos os <paramref name="slugs"/> são estilos existentes.</summary>
    Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct);
}
