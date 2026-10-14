using Microsoft.Extensions.Time.Testing;
using Subsolo.Examples.Sample;

namespace Subsolo.Examples.UnitTests;

/// <summary>
/// SPEC-0005 UT-02 — com o relógio injetado (<see cref="TimeProvider"/>) substituído por um
/// <see cref="FakeTimeProvider"/>, avançar 1 dia faz o serviço enxergar a data seguinte, sem esperar nem depender
/// do relógio real.
/// </summary>
public sealed class ExampleCalendarTests
{
    [Fact]
    [Trait("Spec", "SPEC-0005:UT-02")]
    public void AdvancingTheFakeClockOneDayMakesTheServiceReturnTheNextDate()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 21, 30, 0, TimeSpan.Zero));
        var calendar = new ExampleCalendar(clock);
        calendar.Today().ShouldBe(new DateOnly(2026, 10, 13));

        clock.Advance(TimeSpan.FromDays(1));

        calendar.Today().ShouldBe(new DateOnly(2026, 10, 14));
    }
}
