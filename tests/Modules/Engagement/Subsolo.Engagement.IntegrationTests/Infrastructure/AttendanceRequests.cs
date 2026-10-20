using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0029 — as rotas do §6 ("vou" e contagens) e a preparação dos shows de verdade pela API do Events (SPEC-0023): o
/// admin cria um produtor sem dono, envia um flyer (JPEG gerado com o NetVips do host) e cadastra o show na cidade de
/// desenvolvimento; cancela pela rota de situação.
/// </summary>
internal static class AttendanceRequests
{
    public const string MyAttendances = "/api/v1/me/attendances";

    public const string Upcoming = $"{MyAttendances}?period=upcoming";

    public const string CountsRoute = "/api/v1/attendances/counts";

    /// <summary>Fuso da cidade de desenvolvimento.</summary>
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static string Attend(Guid showId) => $"{MyAttendances}/{showId}";

    public static string Counts(IEnumerable<Guid> showIds) => $"{CountsRoute}?showIds={string.Join(',', showIds)}";

    public static string ShowDetail(string slug) => $"/api/v1/cities/{EngagementApiFactory.City}/shows/{slug}";

    public static string Agenda => $"/api/v1/cities/{EngagementApiFactory.City}/agenda";

    /// <summary><c>GET /me/attendances?period=upcoming</c> com 200 e <c>no-store</c>; devolve os <c>showIds</c>.</summary>
    public static async Task<IReadOnlyList<Guid>> UpcomingAsync(this EngagementApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Upcoming, ct);
        response.ShouldBeNoStore();
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["showIds"], $"corpo fora do §6: {body}");
        return [.. body.GetProperty("showIds").EnumerateArray().Select(id => id.GetGuid())];
    }

    /// <summary><c>GET /attendances/counts</c> com 200; devolve <c>counts</c> (só números) por show.</summary>
    public static async Task<IReadOnlyDictionary<Guid, int>> CountsAsync(
        this EngagementApiClient client, IEnumerable<Guid> showIds, CancellationToken ct)
    {
        using var response = await client.GetAsync(Counts(showIds), ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return ReadCounts(body);
    }

    /// <summary>Lê <c>{counts: {"&lt;showId&gt;": n}}</c>, exigindo só a chave <c>counts</c> e só números inteiros.</summary>
    public static IReadOnlyDictionary<Guid, int> ReadCounts(JsonElement body)
    {
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["counts"], $"a contagem não é só {{counts}}: {body}");
        var counts = new Dictionary<Guid, int>();
        foreach (var entry in body.GetProperty("counts").EnumerateObject())
        {
            entry.Value.ValueKind.ShouldBe(JsonValueKind.Number, $"contagem que não é número: {body}");
            counts[Guid.Parse(entry.Name, CultureInfo.InvariantCulture)] = entry.Value.GetInt32();
        }

        return counts;
    }

    /// <summary>A contagem de um show (0 se ausente).</summary>
    public static async Task<int> CountOfAsync(this EngagementApiClient client, Guid showId, CancellationToken ct) =>
        (await client.CountsAsync([showId], ct)).GetValueOrDefault(showId);

    /// <summary>"Agora" (<paramref name="now"/>) mais <paramref name="days"/> dias às 22h de São Paulo (<c>aaaa-mm-ddThh:mm</c>).</summary>
    public static string LocalAt(DateTimeOffset now, int days) =>
        TimeZoneInfo.ConvertTime(now, SaoPaulo).Date.AddDays(days).AddHours(22)
            .ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// O admin cadastra um show agendado na cidade de desenvolvimento, <paramref name="days"/> dias depois de
    /// <paramref name="now"/> (padrão: agora), com um produtor sem dono e um flyer novo; devolve <c>(id, slug)</c>.
    /// </summary>
    public static async Task<(Guid Id, string Slug)> CreateShowAsync(
        this EngagementApiClient admin, string title, CancellationToken ct, int days = 10, DateTimeOffset? now = null)
    {
        var producer = await admin.CreateApprovedProfileAsync($"Produtora {title}", ct, type: "producer");
        var flyerId = await admin.FlyerAsync(ct);
        using var response = await admin.PostAsync(
            "/api/v1/shows",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["organizerProfileId"] = producer,
                ["title"] = title,
                ["city"] = EngagementApiFactory.City,
                ["startsLocal"] = LocalAt(now ?? DateTimeOffset.UtcNow, days),
                ["venue"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Porão do Rock" },
                ["lineup"] = new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Cova Rasa" } },
                ["styles"] = new[] { EngagementApiFactory.Style },
                ["price"] = "R$ 30",
                ["ticketUrl"] = null,
                ["flyerId"] = flyerId,
            },
            ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return (created.GetProperty("id").GetGuid(), created.GetProperty("slug").GetString().ShouldNotBeNull());
    }

    /// <summary>O admin cancela o show (<c>POST /shows/{id}/status</c>, 200).</summary>
    public static Task CancelShowAsync(this EngagementApiClient admin, Guid showId, CancellationToken ct) =>
        admin.ChangeShowStatusAsync(showId, "cancelled", ct);

    /// <summary>O admin muda a situação do show (<c>cancelled</c> | <c>postponed</c> | <c>scheduled</c>; 200).</summary>
    public static async Task ChangeShowStatusAsync(this EngagementApiClient admin, Guid showId, string status, CancellationToken ct)
    {
        using var response = await admin.PostAsync($"/api/v1/shows/{showId}/status", new { status }, ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK, $"mudar {showId} para {status}: {EngagementApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct))}");
    }

    /// <summary>Um flyer JPEG 300×400 enviado (<c>POST /api/v1/flyers</c>, 201 ou 200); devolve o id.</summary>
    public static async Task<string> FlyerAsync(this EngagementApiClient admin, CancellationToken ct)
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

    /// <summary>Um JPEG 300×400 com ruído (cada chamada um conteúdo diferente).</summary>
    private static byte[] Jpeg()
    {
        using var noise = Image.Gaussnoise(300, 400, mean: 128, sigma: 40, seed: Random.Shared.Next());
        using var image = noise.Cast(Enums.BandFormat.Uchar);
        return image.JpegsaveBuffer(q: 85);
    }
}
