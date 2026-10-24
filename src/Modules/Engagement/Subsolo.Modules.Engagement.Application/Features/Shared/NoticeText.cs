using System.Globalization;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.Shared;

/// <summary>
/// SPEC-0031 — peças de texto dos avisos de show novo e do resumo diário (§6): o line-up unido por " + " (com um teto,
/// para o corpo caber no limite do aviso) e a data e a hora do show no fuso da cidade, em português e sem depender da
/// cultura do servidor (o "sáb" sem o ponto que o ICU põe).
/// </summary>
internal static class NoticeText
{
    /// <summary>O fuso dos resumos e da chave <c>digest:{data}</c> (§6).</summary>
    public const string SaoPaulo = "America/Sao_Paulo";

    /// <summary>Tamanho máximo do line-up num aviso (15 bandas de 80 caracteres passariam do corpo de um aviso).</summary>
    public const int MaxLineupLength = 200;

    /// <summary>A situação do show agendado (<c>ShowSummary.Status</c>).</summary>
    public const string Scheduled = "scheduled";

    private static readonly string[] Days = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    /// <summary>O show ainda vale um aviso: <c>scheduled</c> e não começou em <paramref name="now"/>.</summary>
    public static bool IsUpcoming(ShowSummary show, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(show);
        return show.Status == Scheduled && show.StartsAtUtc > now;
    }

    /// <summary>"Cova Rasa + Peste Negra" (cortado com "…" além de <see cref="MaxLineupLength"/>).</summary>
    public static string Lineup(IReadOnlyList<string> lineup)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        var text = string.Join(" + ", lineup);
        return text.Length <= MaxLineupLength ? text : string.Concat(text.AsSpan(0, MaxLineupLength - 1), "…");
    }

    /// <summary>O instante <paramref name="utc"/> no fuso IANA <paramref name="timeZone"/>.</summary>
    public static DateTime Local(DateTimeOffset utc, string timeZone) =>
        TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime;

    /// <summary>"sáb 14/11 22h00".</summary>
    public static string DayAndTime(DateTime local) =>
        string.Create(CultureInfo.InvariantCulture, $"{Days[(int)local.DayOfWeek]} {local:dd/MM} {local:HH}h{local:mm}");

    /// <summary>"14/11".</summary>
    public static string Day(DateTime local) => local.ToString("dd/MM", CultureInfo.InvariantCulture);
}
