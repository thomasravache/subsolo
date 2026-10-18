using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.CreateCity;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.ListCitiesForAdmin;

/// <summary>SPEC-0017 — as cidades do repositório (ordenadas pelo nome) como <c>{ slug, name, state, timeZone }</c>.</summary>
internal sealed class ListCitiesForAdminQueryHandler(ICityRepository cities)
    : IQueryHandler<ListCitiesForAdminQuery, IReadOnlyList<AdminCityResponse>>
{
    public async Task<Result<IReadOnlyList<AdminCityResponse>>> Handle(
        ListCitiesForAdminQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var all = await cities.ListAsync(cancellationToken);
        IReadOnlyList<AdminCityResponse> response =
            [.. all.Select(city => new AdminCityResponse(city.Slug, city.Name, city.State, city.TimeZone))];
        return Result.Success(response);
    }
}
