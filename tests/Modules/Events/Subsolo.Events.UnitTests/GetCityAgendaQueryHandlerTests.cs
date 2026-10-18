using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Events.UnitTests.Infrastructure;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0008 UT-01 — cidade sem shows: o handler devolve a cidade e uma lista vazia. UT-02 — só <c>to</c>, antes
/// de hoje no fuso da cidade: o período resolvido termina antes de começar e vira <c>agenda.invalid_range</c>.
/// SPEC-0024 (H23-05): construído pelo construtor que fica, com a leitura de shows vazia (<see cref="EmptyShowsReadModel"/>).
/// </summary>
public sealed class GetCityAgendaQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "SPEC-0008:UT-01")]
    public async Task CityWithoutShowsReturnsTheCityAndAnEmptyList()
    {
        var city = City.Create("cidade-exemplo", "Cidade Exemplo", "SP", "America/Sao_Paulo");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
        var handler = new GetCityAgendaQueryHandler(new InMemoryCities(city), clock, EmptyShowsReadModel.Create());

        var result = await handler.Handle(new GetCityAgendaQuery("cidade-exemplo"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.City.ShouldBe(new CityResponse("cidade-exemplo", "Cidade Exemplo", "SP", "America/Sao_Paulo"));
        result.Value.Shows.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public async Task OnlyToBeforeTodayInTheCityTimeZoneFailsWithInvalidRangeCode()
    {
        // 02:30Z de 14/10 ainda é dia 13 em São Paulo: "hoje" (o from padrão) é 2026-10-13.
        var handler = SaoPauloHandlerAt(new DateTimeOffset(2026, 10, 14, 2, 30, 0, TimeSpan.Zero));

        var result = await handler.Handle(
            new GetCityAgendaQuery("cidade-exemplo", From: null, To: new DateOnly(2026, 10, 12)),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("agenda.invalid_range");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public async Task OnlyToEqualToTodayInTheCityTimeZonePasses()
    {
        var handler = SaoPauloHandlerAt(new DateTimeOffset(2026, 10, 14, 2, 30, 0, TimeSpan.Zero));

        var result = await handler.Handle(
            new GetCityAgendaQuery("cidade-exemplo", From: null, To: new DateOnly(2026, 10, 13)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
    }

    private static GetCityAgendaQueryHandler SaoPauloHandlerAt(DateTimeOffset now) =>
        new(
            new InMemoryCities(City.Create("cidade-exemplo", "Cidade Exemplo", "SP", "America/Sao_Paulo")),
            new FakeTimeProvider(now),
            EmptyShowsReadModel.Create());

    private sealed class InMemoryCities(params City[] cities) : ICityRepository
    {
        public Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(cities.FirstOrDefault(city => city.Slug == slug));

        public Task<IReadOnlyList<City>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<City>>([.. cities.OrderBy(city => city.Name, StringComparer.Ordinal)]);
    }
}
