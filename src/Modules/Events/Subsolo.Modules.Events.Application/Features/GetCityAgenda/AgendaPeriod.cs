namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — período da agenda, em datas locais da cidade.</summary>
internal sealed record AgendaPeriod(DateOnly From, DateOnly To)
{
    /// <summary>Duração padrão quando <c>to</c> é omitido.</summary>
    public const int DefaultDays = 60;

    /// <summary>Duração máxima de um período.</summary>
    public const int MaxDays = 180;

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
