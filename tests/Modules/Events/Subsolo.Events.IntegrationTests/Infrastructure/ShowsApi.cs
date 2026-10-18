using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0023 — as rotas do §6 e os passos dos testes do cadastro de show sobre o <see cref="FlyersApiClient"/>: perfis
/// aprovados pela API da SPEC-0021 (pedido do fã + aprovação do admin, que dá o papel <c>artist</c> pela outbox), perfis
/// sem dono criados pelo admin, cidade criada pelo admin (SPEC-0017), flyer enviado (SPEC-0022) e o corpo do show.
/// </summary>
internal static class ShowsApi
{
    public const string Shows = "/api/v1/shows";

    public const string MyShows = "/api/v1/me/shows";

    public const string Export = "/api/v1/me/export";

    public const string Me = "/api/v1/me";

    public const string ProfileRequests = "/api/v1/profiles/requests";

    public const string AdminProfiles = "/api/v1/admin/profiles";

    public const string AdminCities = "/api/v1/admin/cities";

    /// <summary>Cidade semeada em Development (America/Sao_Paulo).</summary>
    public const string City = "cidade-exemplo";

    /// <summary>Estilo semeado pela migração (SPEC-0017).</summary>
    public const string Style = "death";

    /// <summary>Fuso das cidades dos testes.</summary>
    public static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Prazo para a outbox do Profiles e a inbox do Users (a cada 2 s) entregarem o papel <c>artist</c>.</summary>
    private static readonly TimeSpan RoleDeliveryTimeout = TimeSpan.FromSeconds(30);

    public static string Show(Guid id) => $"{Shows}/{id}";

    public static string Status(Guid id) => $"{Shows}/{id}/status";

    public static string Agenda(string city) => $"/api/v1/cities/{city}/agenda";

    public static string MyShowsFor(string period) => $"{MyShows}?period={period}";

    /// <summary>Mutação com corpo JSON e um token anti-forgery novo (pedido com os cookies atuais).</summary>
    public static async Task<HttpResponseMessage> SendJsonAsync(
        this FlyersApiClient client, HttpMethod method, string route, object? body, CancellationToken ct, bool withCsrf = true)
    {
        var token = withCsrf ? await client.CsrfTokenAsync(ct) : null;
        using var content = body is null ? null : JsonContent.Create(body);
        return await client.SendAsync(method, route, ct, content, token);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this FlyersApiClient client, string route, object? body, CancellationToken ct) =>
        client.SendJsonAsync(HttpMethod.Post, route, body, ct);

    /// <summary>"Agora" mais <paramref name="days"/> dias, às <paramref name="hour"/>:<paramref name="minute"/> locais de São Paulo, no formato do §6.</summary>
    public static string LocalAt(DateTimeOffset now, int days, int hour = 22, int minute = 0)
    {
        var local = TimeZoneInfo.ConvertTime(now, SaoPaulo).Date.AddDays(days).AddHours(hour).AddMinutes(minute);
        return local.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>O instante UTC de <paramref name="startsLocal"/> (<c>aaaa-mm-ddThh:mm</c> em São Paulo).</summary>
    public static DateTimeOffset UtcOf(string startsLocal)
    {
        var local = DateTime.ParseExact(startsLocal, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        return new DateTimeOffset(local, SaoPaulo.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>A data local (<c>aaaa-mm-dd</c>) de <paramref name="startsLocal"/>, como vai no slug.</summary>
    public static string DateOf(string startsLocal) => startsLocal[..10];

    /// <summary>Corpo de um show válido (casa por nome livre e line-up com duas bandas por nome, a menos que informados).</summary>
    public static Dictionary<string, object?> ShowBody(
        Guid organizer,
        string flyerId,
        string startsLocal,
        string title = "Noite da Peste II",
        string city = City,
        object? venue = null,
        object[]? lineup = null,
        string[]? styles = null,
        string price = "R$ 30 / R$ 40 na hora",
        string? ticketUrl = "https://ingressos.example/noite-da-peste-ii") => new(StringComparer.Ordinal)
        {
            ["organizerProfileId"] = organizer,
            ["title"] = title,
            ["city"] = city,
            ["startsLocal"] = startsLocal,
            ["venue"] = venue ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Porão do Rock" },
            ["lineup"] = lineup ?? new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Cova Rasa" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Necrocorpo" },
            },
            ["styles"] = styles ?? [Style],
            ["price"] = price,
            ["ticketUrl"] = ticketUrl,
            ["flyerId"] = flyerId,
        };

    /// <summary><c>{ profileId }</c> (casa ou banda com perfil).</summary>
    public static Dictionary<string, object?> WithProfile(Guid profileId) =>
        new(StringComparer.Ordinal) { ["profileId"] = profileId };

    /// <summary><c>{ name }</c> (casa ou banda sem perfil).</summary>
    public static Dictionary<string, object?> Named(string name) =>
        new(StringComparer.Ordinal) { ["name"] = name };

    /// <summary><c>POST /shows</c> com 201 e <c>Location</c>; devolve <c>{id, slug}</c>.</summary>
    public static async Task<(Guid Id, string Slug)> CreateShowAsync(this FlyersApiClient client, object body, CancellationToken ct)
    {
        using var response = await client.PostJsonAsync(Shows, body, ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        var id = created.GetProperty("id").GetGuid();
        response.Headers.Location?.OriginalString.ShouldBe(Show(id), "Location do show criado");
        return (id, created.GetProperty("slug").GetString().ShouldNotBeNull());
    }

    /// <summary>Um flyer enviado (201 ou 200) por <paramref name="client"/>; devolve o id.</summary>
    public static async Task<string> FlyerAsync(this FlyersApiClient client, CancellationToken ct, int seed = 41)
    {
        using var response = await client.UploadAsync(TestImages.Jpeg(300, 400, seed), ct);
        var status = response.StatusCode == HttpStatusCode.OK ? HttpStatusCode.OK : HttpStatusCode.Created;
        return (await FlyerUploaded.ReadAsync(response, status, ct)).FlyerId;
    }

    /// <summary>
    /// Um usuário novo (<paramref name="email"/>) dono do perfil de produtor aprovado <paramref name="name"/>: pede pela
    /// API, o <paramref name="admin"/> aprova e o papel <c>artist</c> chega pela outbox. Com <paramref name="clock"/>
    /// (relógio parado), o relógio anda 2 s a cada consulta para as tarefas periódicas rodarem.
    /// </summary>
    public static async Task<(FlyersApiClient Client, Guid UserId, Guid ProfileId)> ProducerAsync(
        this FlyersApiFactory api, FlyersApiClient admin, string email, string name, CancellationToken ct, FakeTimeProvider? clock = null)
    {
        var (client, userId) = await api.SignedInAsync(email, ct);
        Guid requestId;
        using (var request = await client.PostJsonAsync(ProfileRequests, ProfileBody("producer", name, contact: "@produtora no Instagram"), ct))
        {
            requestId = (await request.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("requestId").GetGuid();
        }

        Guid profileId;
        using (var approve = await admin.PostJsonAsync($"/api/v1/admin/profile-requests/{requestId}/approve", null, ct))
        {
            profileId = (await approve.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).GetProperty("profileId").GetGuid();
        }

        (await Timing.EventuallyAsync(
                async () =>
                {
                    clock?.Advance(TimeSpan.FromSeconds(2));
                    return (await client.RolesAsync(ct)).Contains("artist");
                },
                RoleDeliveryTimeout,
                ct))
            .ShouldBeTrue($"o papel artist não chegou a {email} depois da aprovação do perfil");
        return (client, userId, profileId);
    }

    /// <summary><c>POST /admin/profiles</c> (perfil sem dono) com 201; devolve o id.</summary>
    public static async Task<Guid> OwnerlessAsync(this FlyersApiClient admin, string type, string name, CancellationToken ct, string city = City)
    {
        using var response = await admin.PostJsonAsync(AdminProfiles, ProfileBody(type, name, city: city), ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("profileId").GetGuid();
    }

    /// <summary><c>POST /admin/cities</c> com 201; devolve o slug.</summary>
    public static async Task<string> CityAsync(this FlyersApiClient admin, string name, string state, CancellationToken ct)
    {
        using var response = await admin.PostJsonAsync(
            AdminCities, new { name, state, timeZone = "America/Sao_Paulo", slug = (string?)null }, ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("slug").GetString().ShouldNotBeNull();
    }

    /// <summary><c>GET /cities/{slug}/agenda</c> com 200; devolve a lista <c>shows</c>.</summary>
    public static async Task<JsonElement> AgendaShowsAsync(this FlyersApiClient client, string city, CancellationToken ct)
    {
        using var response = await client.GetAsync(Agenda(city), ct);
        return (await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).GetProperty("shows");
    }

    /// <summary>O item de <paramref name="list"/> com <c>id</c> = <paramref name="id"/>, ou nulo.</summary>
    public static JsonElement? ById(this JsonElement list, Guid id) =>
        list.EnumerateArray()
            .Where(item => item.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String && value.GetGuid() == id)
            .Select(item => (JsonElement?)item)
            .FirstOrDefault();

    /// <summary>O ProblemDetails de validação tem erro no campo <paramref name="field"/> (ou num item dele), sem diferenciar caixa.</summary>
    public static void ShouldHaveFieldErrorStartingWith(this JsonElement problem, string field)
    {
        problem.TryGetProperty("errors", out var errors).ShouldBeTrue($"ProblemDetails sem errors: {problem}");
        errors.EnumerateObject()
            .Select(property => property.Name)
            .ShouldContain(name => name.StartsWith(field, StringComparison.OrdinalIgnoreCase), $"errors sem o campo {field}: {errors}");
    }

    private static Dictionary<string, object?> ProfileBody(string type, string name, string city = City, string? contact = null)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["name"] = name,
            ["city"] = city,
            ["styles"] = type == "band" ? new[] { Style } : Array.Empty<string>(),
            ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
        };
        if (contact is not null)
        {
            body["contact"] = contact;
        }

        return body;
    }
}
