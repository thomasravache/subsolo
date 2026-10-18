using Subsolo.Modules.Events.Application.Features.CreateShow;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0023 UT-04 — o início do show (data e hora locais da cidade) vai para UTC e volta para a mesma hora local:
/// 14/11/2026 22:00 em <c>America/Sao_Paulo</c> (UTC−3) é 01:00 UTC de 15/11; em <c>America/New_York</c>, 01:30 de
/// 01/11/2026 acontece duas vezes (fim do horário de verão) e qualquer das duas conversões é um instante real que volta
/// para 01:30; logo depois do início do horário de verão (08/03/2026 03:30, UTC−4) dá 07:30 UTC.
/// </summary>
public sealed class ShowLocalTimeTests
{
    [Fact]
    [Trait("Spec", "SPEC-0023:UT-04")]
    public void TenPmInSaoPauloIsOneAmUtcTheNextDayAndBack()
    {
        var local = new DateTime(2026, 11, 14, 22, 0, 0, DateTimeKind.Unspecified);

        var utc = ShowLocalTime.ToUtc(local, "America/Sao_Paulo");

        utc.ShouldBe(new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero));
        utc.Offset.ShouldBe(TimeSpan.Zero, "o início é guardado em UTC");
        ShowLocalTime.ToLocal(utc, "America/Sao_Paulo").ShouldBe(local);
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-04")]
    public void AnAmbiguousLocalTimeAtTheEndOfDaylightSavingConvertsToARealInstantAndBack()
    {
        var local = new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);

        var utc = ShowLocalTime.ToUtc(local, "America/New_York");

        utc.UtcDateTime.ShouldBeOneOf(
            new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc));
        ShowLocalTime.ToLocal(utc, "America/New_York").ShouldBe(local);
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-04")]
    public void RightAfterDaylightSavingStartsTheNewOffsetIsUsed()
    {
        var local = new DateTime(2026, 3, 8, 3, 30, 0, DateTimeKind.Unspecified);

        var utc = ShowLocalTime.ToUtc(local, "America/New_York");

        utc.ShouldBe(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero));
        ShowLocalTime.ToLocal(utc, "America/New_York").ShouldBe(local);
    }
}
