namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0008 — período da agenda, em datas locais da cidade.</summary>
internal sealed record AgendaPeriod(DateOnly From, DateOnly To)
{
    /// <summary>Duração padrão quando <c>to</c> é omitido.</summary>
    public const int DefaultDays = 60;

    /// <summary>Duração máxima de um período.</summary>
    public const int MaxDays = 180;

    /// <summary>
    /// Completa o período pedido: <c>from</c> omitido = hoje no fuso <paramref name="timeZone"/> (pelo
    /// <paramref name="clock"/>); <c>to</c> omitido = <c>from</c> + <see cref="DefaultDays"/> dias.
    /// </summary>
    public static AgendaPeriod Resolve(DateOnly? from, DateOnly? to, TimeZoneInfo timeZone, TimeProvider clock) =>
        throw new NotImplementedException();
}
