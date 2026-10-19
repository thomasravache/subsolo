namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — período da agenda, em datas locais da cidade.</summary>
internal sealed record AgendaPeriod(DateOnly From, DateOnly To)
{
    /// <summary>Duração padrão quando <c>to</c> é omitido.</summary>
    public const int DefaultDays = 60;

    /// <summary>Duração máxima de um período.</summary>
    public const int MaxDays = 180;

    /// <summary>SPEC-0025 — primeira data aceita em <c>from</c>/<c>to</c>.</summary>
    public static readonly DateOnly MinDate = new(2000, 1, 1);

    /// <summary>
    /// SPEC-0025 — última data aceita em <c>from</c>/<c>to</c> (longe de <see cref="DateOnly.MaxValue"/>: o período padrão
    /// e o fim do dia somam dias sem estourar).
    /// </summary>
    public static readonly DateOnly MaxDate = new(2100, 12, 31);

    /// <summary>SPEC-0025 — <paramref name="date"/> entre <see cref="MinDate"/> e <see cref="MaxDate"/>.</summary>
    public static bool IsWithinBounds(DateOnly date) => date >= MinDate && date <= MaxDate;

    /// <summary><c>from ≤ to</c> e no máximo <see cref="MaxDays"/> dias entre as duas datas.</summary>
    public bool IsValid => IsValidRange(From, To);

    /// <summary>
    /// Completa o período pedido: <c>from</c> omitido = hoje no fuso <paramref name="timeZone"/> (pelo
    /// <paramref name="clock"/>); <c>to</c> omitido = <c>from</c> + <see cref="DefaultDays"/> dias.
    /// </summary>
    public static AgendaPeriod Resolve(DateOnly? from, DateOnly? to, TimeZoneInfo timeZone, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(clock);
        var start = from ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
        return new AgendaPeriod(start, to ?? start.AddDays(DefaultDays));
    }

    /// <summary><c>from ≤ to</c> e no máximo <see cref="MaxDays"/> dias entre as duas datas.</summary>
    public static bool IsValidRange(DateOnly from, DateOnly to) =>
        to >= from && to.DayNumber - from.DayNumber <= MaxDays;
}
