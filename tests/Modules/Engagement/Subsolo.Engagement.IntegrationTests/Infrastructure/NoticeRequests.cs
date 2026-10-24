using System.Globalization;
using System.Net;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0031 — preparação dos avisos de show novo e do resumo diário pela API: o show cadastrado pelo admin na cidade de
/// desenvolvimento (com uma banda com perfil no line-up, se pedido) e as preferências de aviso do fã (SPEC-0030).
/// </summary>
internal static class NoticeRequests
{
    public const string Preferences = "/api/v1/me/notification-preferences";

    /// <summary>Nome da cidade de desenvolvimento (<c>DevelopmentCitySeed</c>).</summary>
    public const string CityName = "Cidade Exemplo";

    /// <summary>Casa (por nome) dos shows de teste.</summary>
    public const string Venue = "Porão do Rock";

    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>
    /// O admin cadastra um show agendado na cidade de desenvolvimento, <paramref name="days"/> dias depois de
    /// <paramref name="now"/> às 22h (São Paulo), com um produtor sem dono, a casa <see cref="Venue"/> e o line-up
    /// <paramref name="band"/> (perfil) ou "Cova Rasa" (por nome); devolve id, slug e o início local.
    /// </summary>
    public static async Task<CreatedShow> CreateShowAsync(
        this EngagementApiClient admin, string title, DateTimeOffset now, int days, CancellationToken ct, Guid? band = null)
    {
        var producer = await admin.CreateApprovedProfileAsync($"Produtora {title}", ct, type: "producer");
        var flyerId = await admin.FlyerAsync(ct);
        var startsLocal = AttendanceRequests.LocalAt(now, days);
        var lineup = band is { } bandId
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["profileId"] = bandId }
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Cova Rasa" };
        using var response = await admin.PostAsync(
            "/api/v1/shows",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["organizerProfileId"] = producer,
                ["title"] = title,
                ["city"] = EngagementApiFactory.City,
                ["startsLocal"] = startsLocal,
                ["venue"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = Venue },
                ["lineup"] = new object[] { lineup },
                ["styles"] = new[] { EngagementApiFactory.Style },
                ["price"] = "R$ 30",
                ["ticketUrl"] = null,
                ["flyerId"] = flyerId,
            },
            ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return new CreatedShow(
            created.GetProperty("id").GetGuid(),
            created.GetProperty("slug").GetString().ShouldNotBeNull(),
            DateTime.ParseExact(startsLocal, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
    }

    /// <summary>O início (UTC) de um show local de São Paulo.</summary>
    public static DateTimeOffset UtcOf(DateTime startsLocal) =>
        new(TimeZoneInfo.ConvertTimeToUtc(startsLocal, SaoPaulo), TimeSpan.Zero);

    /// <summary><c>PUT /me/notification-preferences</c> com os quatro interruptores (200).</summary>
    public static async Task SetPreferencesAsync(
        this EngagementApiClient fan, bool newShowPush, bool dailyDigestEmail, CancellationToken ct)
    {
        using var response = await fan.MutateAsync(
            HttpMethod.Put,
            Preferences,
            new { newShowPush, dailyDigestEmail, reminderPush = false, reminderEmail = false },
            ct);
        await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }
}

/// <summary>SPEC-0031 — um show cadastrado: id, slug e o início local (São Paulo).</summary>
internal sealed record CreatedShow(Guid Id, string Slug, DateTime StartsLocal)
{
    /// <summary>A data do show como no resumo (<c>dd/MM</c>).</summary>
    public string Day => StartsLocal.ToString("dd/MM", CultureInfo.InvariantCulture);
}
