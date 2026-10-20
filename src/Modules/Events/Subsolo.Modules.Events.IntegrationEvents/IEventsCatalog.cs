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

    /// <summary>
    /// SPEC-0029 — a situação e o início do show <paramref name="showId"/> (o <c>Engagement</c> valida o "vou" com ela), ou
    /// <see langword="null"/> se o show não existe.
    /// </summary>
    Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct);
}

/// <summary>
/// SPEC-0029 — situação (<c>scheduled</c> | <c>cancelled</c> | <c>postponed</c>) e início (UTC) de um show, para outros
/// módulos.
/// </summary>
public sealed record ShowState(string Status, DateTimeOffset StartsAtUtc);
