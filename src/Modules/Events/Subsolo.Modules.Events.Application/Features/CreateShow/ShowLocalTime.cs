using System.Globalization;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 (UT-04) — o início do show chega como data e hora locais da cidade e é guardado em UTC; a agenda e as
/// telas mostram de volta a hora local. Hora que não existe no fuso (lacuna do horário de verão) é recusada (Emenda v2,
/// e); hora ambígua (fim do horário de verão) vira um dos dois instantes, e a volta dá a mesma hora local.
/// </summary>
public static class ShowLocalTime
{
    /// <summary>Formato do início no corpo: <c>aaaa-mm-ddThh:mm</c>.</summary>
    public const string Format = "yyyy-MM-dd'T'HH:mm";

    /// <summary>O instante UTC da hora local <paramref name="local"/> no fuso <paramref name="timeZoneId"/> (IANA).</summary>
    public static DateTimeOffset ToUtc(DateTime local, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified))
        {
            throw new ArgumentException($"{local:yyyy-MM-dd HH:mm} não existe no fuso {timeZoneId}.", nameof(local));
        }

        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    /// <summary>A hora local de <paramref name="utc"/> no fuso <paramref name="timeZoneId"/> (IANA).</summary>
    public static DateTime ToLocal(DateTimeOffset utc, string timeZoneId) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime,
            DateTimeKind.Unspecified);

    /// <summary><paramref name="value"/> no formato <see cref="Format"/>, ou <see langword="null"/>.</summary>
    public static DateTime? Parse(string? value) =>
        DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? DateTime.SpecifyKind(local, DateTimeKind.Unspecified)
            : null;

    /// <summary>O instante UTC de <paramref name="value"/> no fuso, ou <see langword="null"/> se o formato ou a hora não valem.</summary>
    public static DateTimeOffset? TryToUtc(string? value, string timeZoneId)
    {
        if (Parse(value) is not { } local)
        {
            return null;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return timeZone.IsInvalidTime(local) ? null : ToUtc(local, timeZoneId);
    }
}
