namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 (UT-04) — o início do show chega como data e hora locais da cidade e é guardado em UTC; a agenda e as
/// telas mostram de volta a hora local.
/// </summary>
public static class ShowLocalTime
{
    /// <summary>O instante UTC da hora local <paramref name="local"/> no fuso <paramref name="timeZoneId"/> (IANA).</summary>
    public static DateTimeOffset ToUtc(DateTime local, string timeZoneId) =>
        throw new NotImplementedException("SPEC-0023: ShowLocalTime.ToUtc ainda não implementado.");

    /// <summary>A hora local de <paramref name="utc"/> no fuso <paramref name="timeZoneId"/> (IANA).</summary>
    public static DateTime ToLocal(DateTimeOffset utc, string timeZoneId) =>
        throw new NotImplementedException("SPEC-0023: ShowLocalTime.ToLocal ainda não implementado.");
}
