using Microsoft.Extensions.Time.Testing;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0008 UT-03 — sem <c>from</c>/<c>to</c>, o período vai de hoje no fuso da cidade até +60 dias: às 02:30Z de
/// 14/10/2026 ainda é dia 13 em São Paulo, então <c>from</c> = 2026-10-13 e <c>to</c> = 2026-12-12.
/// </summary>
public sealed class AgendaPeriodTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 2, 30, 0, TimeSpan.Zero));

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-03")]
    public void OmittedPeriodStartsTodayInTheCityTimeZoneAndLasts60Days()
    {
        var period = AgendaPeriod.Resolve(from: null, to: null, SaoPaulo, _clock);

        period.From.ShouldBe(new DateOnly(2026, 10, 13));
        period.To.ShouldBe(new DateOnly(2026, 12, 12));
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-03")]
    public void OmittedToIs60DaysAfterTheGivenFrom()
    {
        var period = AgendaPeriod.Resolve(new DateOnly(2026, 11, 1), to: null, SaoPaulo, _clock);

        period.From.ShouldBe(new DateOnly(2026, 11, 1));
        period.To.ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-03")]
    public void GivenPeriodIsKept()
    {
        var period = AgendaPeriod.Resolve(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), SaoPaulo, _clock);

        period.ShouldBe(new AgendaPeriod(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
    }
}
