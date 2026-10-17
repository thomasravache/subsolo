using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.ListCities;

/// <summary>SPEC-0011 — cidades com agenda no Subsolo, ordenadas pelo nome (lista do seletor de cidade).</summary>
public sealed record ListCitiesQuery : IQuery<IReadOnlyList<CitySummaryResponse>>;

/// <summary>SPEC-0011 — cidade na lista pública: o slug da URL (<c>/{slug}</c>) e o nome exibido.</summary>
public sealed record CitySummaryResponse(string Slug, string Name);
