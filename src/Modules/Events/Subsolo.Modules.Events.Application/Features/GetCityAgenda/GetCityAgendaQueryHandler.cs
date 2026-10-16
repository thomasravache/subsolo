using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — devolve a cidade e os shows do período (lista vazia nesta spec); cidade inexistente →
/// <see cref="AgendaErrors.CityNotFound"/> (NotFound); período resolvido inválido (ex.: só <c>to</c>, antes de
/// hoje) → <see cref="AgendaErrors.InvalidRange"/> (Validation).
/// </summary>
internal sealed class GetCityAgendaQueryHandler(ICityRepository cities, TimeProvider clock)
    : IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>
{
    public async Task<Result<CityAgendaResponse>> Handle(GetCityAgendaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var city = await cities.GetBySlugAsync(query.Slug, cancellationToken);
        if (city is null)
        {
            return Result.Failure<CityAgendaResponse>(
                Error.NotFound(AgendaErrors.CityNotFound, $"Nenhuma cidade com o slug \"{query.Slug}\"."));
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
        var period = AgendaPeriod.Resolve(query.From, query.To, timeZone, clock);
        if (!period.IsValid)
        {
            return Result.Failure<CityAgendaResponse>(AgendaErrors.InvalidRangeError(period));
        }

        // Os shows do período chegam com o cadastro de shows; até lá a agenda é sempre vazia.
        return Result.Success(new CityAgendaResponse(
            new CityResponse(city.Slug, city.Name, city.State, city.TimeZone),
            []));
    }
}
