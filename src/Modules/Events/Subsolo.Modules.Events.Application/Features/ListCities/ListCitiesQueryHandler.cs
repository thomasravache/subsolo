using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.ListCities;

/// <summary>SPEC-0011 — devolve todas as cidades cadastradas como <c>{ slug, name }</c>, na ordem do repositório (nome).</summary>
internal sealed class ListCitiesQueryHandler(ICityRepository cities)
    : IQueryHandler<ListCitiesQuery, IReadOnlyList<CitySummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CitySummaryResponse>>> Handle(
        ListCitiesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var all = await cities.ListAsync(cancellationToken);
        IReadOnlyList<CitySummaryResponse> response = [.. all.Select(city => new CitySummaryResponse(city.Slug, city.Name))];
        return Result.Success(response);
    }
}
