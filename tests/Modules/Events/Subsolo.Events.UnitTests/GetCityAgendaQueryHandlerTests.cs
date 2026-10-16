using Microsoft.Extensions.Time.Testing;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Events.UnitTests;

/// <summary>SPEC-0008 UT-01 — cidade sem shows: o handler devolve a cidade e uma lista vazia.</summary>
public sealed class GetCityAgendaQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "SPEC-0008:UT-01")]
    public async Task CityWithoutShowsReturnsTheCityAndAnEmptyList()
    {
        var city = City.Create("cidade-exemplo", "Cidade Exemplo", "SP", "America/Sao_Paulo");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
        var handler = new GetCityAgendaQueryHandler(new InMemoryCities(city), clock);

        var result = await handler.Handle(new GetCityAgendaQuery("cidade-exemplo"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.City.ShouldBe(new CityResponse("cidade-exemplo", "Cidade Exemplo", "SP", "America/Sao_Paulo"));
        result.Value.Shows.ShouldBeEmpty();
    }

    private sealed class InMemoryCities(params City[] cities) : ICityRepository
    {
        public Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(cities.FirstOrDefault(city => city.Slug == slug));
    }
}
