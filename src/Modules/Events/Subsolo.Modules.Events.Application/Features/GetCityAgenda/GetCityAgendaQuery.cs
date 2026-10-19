using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — agenda da cidade <paramref name="Slug"/> entre <paramref name="From"/> e <paramref name="To"/>
/// (padrão: de hoje, no fuso da cidade, até +60 dias; no máximo 180 dias). SPEC-0025 — <paramref name="Styles"/>: só os
/// shows com algum desses estilos (slugs; os inexistentes são ignorados).
/// </summary>
public sealed record GetCityAgendaQuery(
    string Slug,
    DateOnly? From = null,
    DateOnly? To = null,
    IReadOnlyList<string>? Styles = null)
    : IQuery<CityAgendaResponse>
{
    /// <summary>Formato do slug da cidade na URL.</summary>
    public const string SlugPattern = "^[a-z0-9-]{2,60}$";

    /// <summary>SPEC-0025 — formato de cada slug de estilo do filtro (o mesmo dos estilos cadastrados).</summary>
    public const string StylePattern = "^[a-z0-9-]{2,60}$";

    /// <summary>SPEC-0025 — quantos estilos o filtro aceita.</summary>
    public const int MaxStyles = 20;

    /// <summary>
    /// SPEC-0025 — os slugs de <c>styles=death,grind</c>: separados por vírgula, sem espaços nas pontas, sem vazios
    /// nem repetidos (o formato de cada um é conferido pelo validador).
    /// </summary>
    public static IReadOnlyList<string> ParseStyles(string? styles) =>
        string.IsNullOrWhiteSpace(styles)
            ? []
            : [.. styles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
}
