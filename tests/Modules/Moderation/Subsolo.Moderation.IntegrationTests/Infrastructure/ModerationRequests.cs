using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;

namespace Subsolo.Moderation.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0033 — as rotas do §6 (denúncia, fila e decisões) e das leituras do Events que o ocultar afeta (agenda, página
/// do show, shows do perfil, "Meus shows", edição), e a preparação dos shows de verdade pela API do Events (copiada do
/// <c>AttendanceRequests</c> da SPEC-0029): o admin cria um produtor sem dono, envia um flyer (JPEG gerado com o NetVips
/// do host) e cadastra o show na cidade de desenvolvimento — o admin é o "dono" do show em "Meus shows".
/// </summary>
internal static class ModerationRequests
{
    public const string AdminReports = "/api/v1/admin/reports";

    public const string Export = "/api/v1/me/export";

    public const string Me = "/api/v1/me";

    public const string MyShowsUpcoming = "/api/v1/me/shows?period=upcoming";

    public const string Agenda = $"/api/v1/cities/{ModerationApiFactory.City}/agenda";

    /// <summary>As chaves de cada item da fila (§6; SPEC-0037 Emenda 1: mais <c>timeZone</c>) — nada de quem denunciou.</summary>
    public static readonly string[] QueueItemKeys =
    [
        "showId", "title", "citySlug", "slug", "startsAtUtc", "venueName", "counts", "latestDetails", "firstReportedAtUtc",

        // SPEC-0037 (Emenda 1, H33-06): o fuso da cidade do show, campo aditivo.
        "timeZone",
    ];

    /// <summary>As chaves de <c>counts</c> (§6).</summary>
    public static readonly string[] CountKeys = ["fake", "cancelled_unannounced", "wrong_info", "other"];

    /// <summary>Fuso da cidade de desenvolvimento.</summary>
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static string Reports(Guid showId) => $"/api/v1/shows/{showId}/reports";

    public static string Keep(Guid showId) => $"{AdminReports}/{showId}/keep";

    public static string Hide(Guid showId) => $"{AdminReports}/{showId}/hide";

    public static string Show(Guid showId) => $"/api/v1/shows/{showId}";

    public static string ShowStatus(Guid showId) => $"/api/v1/shows/{showId}/status";

    public static string Detail(string slug) => $"/api/v1/cities/{ModerationApiFactory.City}/shows/{slug}";

    public static string ProfileShows(Guid profileId) => $"/api/v1/shows?profileId={profileId}&period=upcoming";

    /// <summary>"Agora" mais <paramref name="days"/> dias às 22h de São Paulo (<c>aaaa-mm-ddThh:mm</c>).</summary>
    public static string LocalAt(DateTimeOffset now, int days) =>
        TimeZoneInfo.ConvertTime(now, SaoPaulo).Date.AddDays(days).AddHours(22)
            .ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Corpo de <c>POST /shows/{id}/reports</c>.</summary>
    public static object ReportBody(string? reason, string? details = null) => new { reason, details };

    /// <summary>
    /// O admin cadastra um show agendado na cidade de desenvolvimento, <paramref name="days"/> dias depois de agora, com
    /// um produtor sem dono e um flyer novo; devolve o que a edição precisa reenviar.
    /// </summary>
    public static async Task<ModerationShow> CreateShowAsync(
        this ModerationApiClient admin, string title, CancellationToken ct, int days = 10, string city = ModerationApiFactory.City)
    {
        var producer = await admin.CreateApprovedProfileAsync($"Produtora {title}", ct);
        var flyerId = await admin.FlyerAsync(ct);
        var startsLocal = LocalAt(DateTimeOffset.UtcNow, days);
        using var response = await admin.PostAsync("/api/v1/shows", ShowBody(producer, title, startsLocal, flyerId, city), ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return new ModerationShow(
            created.GetProperty("id").GetGuid(),
            created.GetProperty("slug").GetString().ShouldNotBeNull(),
            title,
            producer,
            flyerId,
            startsLocal);
    }

    /// <summary>O corpo de cadastro/edição do show (§6 da SPEC-0023).</summary>
    public static Dictionary<string, object?> ShowBody(
        Guid organizer, string title, string startsLocal, string flyerId, string city = ModerationApiFactory.City) =>
        new(StringComparer.Ordinal)
        {
            ["organizerProfileId"] = organizer,
            ["title"] = title,
            ["city"] = city,
            ["startsLocal"] = startsLocal,
            ["venue"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Porão do Rock" },
            ["lineup"] = new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Cova Rasa" } },
            ["styles"] = new[] { ModerationApiFactory.Style },
            ["price"] = "R$ 30",
            ["ticketUrl"] = null,
            ["flyerId"] = flyerId,
        };

    /// <summary><c>POST /admin/profiles</c> (perfil sem dono, aprovado) com 201; devolve o id.</summary>
    public static async Task<Guid> CreateApprovedProfileAsync(
        this ModerationApiClient admin, string name, CancellationToken ct, string type = "producer")
    {
        using var response = await admin.PostAsync(
            "/api/v1/admin/profiles",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["name"] = name,
                ["city"] = ModerationApiFactory.City,
                ["styles"] = type == "band" ? new[] { ModerationApiFactory.Style } : Array.Empty<string>(),
                ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
            },
            ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("profileId").GetGuid();
    }

    /// <summary>
    /// SPEC-0037 — <c>POST /admin/cities</c> (nome, UF e fuso) com 201; devolve o slug gerado (copiado do
    /// <c>ShowsApi</c> do Events).
    /// </summary>
    public static async Task<string> CreateCityAsync(
        this ModerationApiClient admin, string name, string state, string timeZone, CancellationToken ct)
    {
        using var response = await admin.PostAsync(
            "/api/v1/admin/cities", new { name, state, timeZone, slug = (string?)null }, ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("slug").GetString().ShouldNotBeNull();
    }

    /// <summary>Um flyer JPEG 300×400 enviado (<c>POST /api/v1/flyers</c>, 201 ou 200); devolve o id.</summary>
    public static async Task<string> FlyerAsync(this ModerationApiClient admin, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Jpeg());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(part, "file", "flyer.jpg");
        using var response = await admin.SendContentAsync(HttpMethod.Post, "/api/v1/flyers", form, ct);
        var status = response.StatusCode == HttpStatusCode.OK ? HttpStatusCode.OK : HttpStatusCode.Created;
        var body = await response.ShouldBeJsonAsync(status, ct);
        return body.GetProperty("flyerId").GetString().ShouldNotBeNull();
    }

    /// <summary><c>POST /shows/{id}/reports</c> com 201 <c>{ id }</c>; devolve o id da denúncia.</summary>
    public static async Task<Guid> ReportAsync(
        this ModerationApiClient fan, Guid showId, string reason, CancellationToken ct, string? details = null)
    {
        using var response = await fan.PostAsync(Reports(showId), ReportBody(reason, details), ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        created.EnumerateObject().Select(property => property.Name).ShouldBe(["id"], $"201 fora do §6: {created}");
        return created.GetProperty("id").GetGuid();
    }

    /// <summary><c>GET /admin/reports</c> com 200, <c>no-store</c> e uma lista; devolve a lista.</summary>
    public static async Task<JsonElement> QueueAsync(this ModerationApiClient admin, CancellationToken ct)
    {
        using var response = await admin.GetAsync(AdminReports, ct);
        response.ShouldBeNoStore();
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.ValueKind.ShouldBe(JsonValueKind.Array, body.ToString());
        return body;
    }

    /// <summary><c>POST /admin/reports/{showId}/keep</c> com 204.</summary>
    public static async Task KeepAsync(this ModerationApiClient admin, Guid showId, CancellationToken ct)
    {
        using var response = await admin.PostAsync(Keep(showId), null, ct);
        await response.ShouldBeNoContentAsync(ct);
    }

    /// <summary><c>POST /admin/reports/{showId}/hide</c> com o motivo interno; 204.</summary>
    public static async Task HideAsync(this ModerationApiClient admin, Guid showId, string? note, CancellationToken ct)
    {
        using var response = await admin.PostAsync(Hide(showId), new { note }, ct);
        await response.ShouldBeNoContentAsync(ct);
    }

    /// <summary>Exige 204 sem corpo.</summary>
    public static async Task ShouldBeNoContentAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {ModerationApiClient.Excerpt(body)}");
        body.ShouldBeEmpty();
    }

    /// <summary><c>GET /cities/{cidade}/agenda</c> com 200; devolve os ids dos shows.</summary>
    public static async Task<IReadOnlyList<Guid>> AgendaShowIdsAsync(this ModerationApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Agenda, ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return [.. body.GetProperty("shows").EnumerateArray().Select(show => show.GetProperty("id").GetGuid())];
    }

    /// <summary>O item de <paramref name="list"/> com <c>{property} = id</c>, ou nulo.</summary>
    public static JsonElement? ById(this JsonElement list, string property, Guid id) =>
        list.EnumerateArray()
            .Where(item => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetGuid() == id)
            .Select(item => (JsonElement?)item)
            .FirstOrDefault();

    /// <summary>Um JPEG 300×400 com ruído (cada chamada um conteúdo diferente).</summary>
    private static byte[] Jpeg()
    {
        using var noise = Image.Gaussnoise(300, 400, mean: 128, sigma: 40, seed: Random.Shared.Next());
        using var image = noise.Cast(Enums.BandFormat.Uchar);
        return image.JpegsaveBuffer(q: 85);
    }
}

/// <summary>SPEC-0033 — um show cadastrado pelo admin: o que a edição reenvia e o início local (São Paulo).</summary>
internal sealed record ModerationShow(Guid Id, string Slug, string Title, Guid OrganizerProfileId, string FlyerId, string StartsLocal);
