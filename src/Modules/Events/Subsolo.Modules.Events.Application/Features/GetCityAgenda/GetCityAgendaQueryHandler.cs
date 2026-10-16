using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — devolve a cidade e os shows do período (lista vazia nesta spec); cidade inexistente →
/// <see cref="AgendaErrors.CityNotFound"/> (NotFound).
/// </summary>
internal sealed class GetCityAgendaQueryHandler(ICityRepository cities, TimeProvider clock)
    : IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>
{
    public Task<Result<CityAgendaResponse>> Handle(GetCityAgendaQuery query, CancellationToken cancellationToken)
    {
        _ = (cities, clock);
        throw new NotImplementedException();
    }
}
