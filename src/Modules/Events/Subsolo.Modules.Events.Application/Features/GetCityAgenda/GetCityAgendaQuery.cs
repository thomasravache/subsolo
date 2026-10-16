using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — agenda da cidade <paramref name="Slug"/> entre <paramref name="From"/> e <paramref name="To"/>
/// (padrão: de hoje, no fuso da cidade, até +60 dias; no máximo 180 dias).
/// </summary>
public sealed record GetCityAgendaQuery(string Slug, DateOnly? From = null, DateOnly? To = null)
    : IQuery<CityAgendaResponse>
{
    /// <summary>Formato do slug da cidade na URL.</summary>
    public const string SlugPattern = "^[a-z0-9-]{2,60}$";
}
