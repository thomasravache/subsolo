namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — códigos de erro da agenda (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class AgendaErrors
{
    /// <summary>400: slug fora de <c>^[a-z0-9-]{2,60}$</c>.</summary>
    public const string InvalidSlug = "agenda.invalid_slug";

    /// <summary>400: <c>to &lt; from</c> ou período maior que <see cref="AgendaPeriod.MaxDays"/> dias.</summary>
    public const string InvalidRange = "agenda.invalid_range";

    /// <summary>404: nenhuma cidade com o slug.</summary>
    public const string CityNotFound = "agenda.city_not_found";
}
