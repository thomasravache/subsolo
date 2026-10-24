namespace Subsolo.Modules.Events.IntegrationEvents;

/// <summary>
/// SPEC-0033 (Emenda 2) — leitura em lote dos dados de exibição dos shows para outros módulos (a fila de denúncias do
/// Moderation): só dos shows encontrados e visíveis (ignora os ocultos pela moderação).
/// </summary>
public interface IShowHeadlines
{
    Task<IReadOnlyDictionary<Guid, ShowHeadline>> GetAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct);
}

/// <summary>SPEC-0033 (Emenda 2) — título, cidade (slug), slug, início (UTC) e casa de um show.</summary>
public sealed record ShowHeadline(
    Guid ShowId,
    string Title,
    string CitySlug,
    string Slug,
    DateTimeOffset StartsAtUtc,
    string VenueName);
