using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — códigos de erro da agenda (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class AgendaErrors
{
    /// <summary>400: slug fora de <c>^[a-z0-9-]{2,60}$</c>.</summary>
    public const string InvalidSlug = "agenda.invalid_slug";

    /// <summary>
    /// 400: <c>to &lt; from</c> ou período maior que <see cref="AgendaPeriod.MaxDays"/> dias; SPEC-0025: também data mal
    /// formada em <c>from</c>/<c>to</c>.
    /// </summary>
    public const string InvalidRange = "agenda.invalid_range";

    /// <summary>SPEC-0025 — 400: slug de estilo fora do padrão ou estilos demais em <c>styles</c>.</summary>
    public const string InvalidStyles = "agenda.invalid_styles";

    /// <summary>404: nenhuma cidade com o slug.</summary>
    public const string CityNotFound = "agenda.city_not_found";

    /// <summary>
    /// SPEC-0025 — <c>from</c> e/ou <c>to</c> que não são datas <c>aaaa-mm-dd</c>: os erros apontam os campos que falharam
    /// (<see cref="GetCityAgendaQuery.From"/>, <see cref="GetCityAgendaQuery.To"/>; nenhum identificado → os dois).
    /// </summary>
    public static Error MalformedDateError(IReadOnlyCollection<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        string[] failed = fields.Count > 0 ? [.. fields] : [nameof(GetCityAgendaQuery.From), nameof(GetCityAgendaQuery.To)];
        return Error.Validation(
            InvalidRange,
            "Período da agenda inválido.",
            failed.ToDictionary(
                field => field,
                field => new[] { $"\"{field.ToLowerInvariant()}\" deve ser uma data no formato aaaa-mm-dd." },
                StringComparer.Ordinal));
    }

    /// <summary>Erro de validação do período já resolvido (o validador só confere quando as duas datas vêm na consulta).</summary>
    internal static Error InvalidRangeError(AgendaPeriod period) =>
        Error.Validation(
            InvalidRange,
            "Período da agenda inválido.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(GetCityAgendaQuery.To)] =
                    [$"O período de {period.From:yyyy-MM-dd} a {period.To:yyyy-MM-dd} deve ter no máximo {AgendaPeriod.MaxDays} dias e \"to\" igual ou depois de \"from\"."],
            });
}
