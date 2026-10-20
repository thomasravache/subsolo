using System.Net;
using System.Text.Json;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>SPEC-0027 — as rotas do §6 e os passos de preparação (perfis pela API do admin, SPEC-0021) usados pelos testes.</summary>
internal static class EngagementRequests
{
    public const string MyFollows = "/api/v1/me/follows";

    public const string Export = "/api/v1/me/export";

    public const string Me = "/api/v1/me";

    public const string AdminProfiles = "/api/v1/admin/profiles";

    public const string ProfileRequests = "/api/v1/profiles/requests";

    public static string FollowCity(string slug) => $"{MyFollows}/cities/{slug}";

    public static string FollowProfile(Guid profileId) => $"{MyFollows}/profiles/{profileId}";

    public static string ProfileFollowers(Guid profileId) => $"/api/v1/profiles/{profileId}/followers";

    public static string CityFollowers(string slug) => $"/api/v1/cities/{slug}/followers";

    /// <summary><c>PUT</c> (seguir) com o <c>X-CSRF</c>.</summary>
    public static Task<HttpResponseMessage> PutAsync(this EngagementApiClient client, string route, CancellationToken ct) =>
        client.MutateAsync(HttpMethod.Put, route, null, ct);

    /// <summary><c>DELETE</c> (deixar de seguir) com o <c>X-CSRF</c>.</summary>
    public static Task<HttpResponseMessage> DeleteAsync(this EngagementApiClient client, string route, CancellationToken ct) =>
        client.MutateAsync(HttpMethod.Delete, route, null, ct);

    /// <summary>Exige 204 sem corpo.</summary>
    public static async Task ShouldBeNoContentAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {EngagementApiClient.Excerpt(body)}");
        body.ShouldBeEmpty();
    }

    /// <summary><c>PUT</c> ou <c>DELETE</c> com 204.</summary>
    public static async Task ShouldSucceedAsync(this Task<HttpResponseMessage> send, CancellationToken ct)
    {
        using var response = await send;
        await response.ShouldBeNoContentAsync(ct);
    }

    /// <summary><c>GET /me/follows</c> com 200 e <c>no-store</c>; devolve o corpo.</summary>
    public static async Task<JsonElement> MyFollowsAsync(this EngagementApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(MyFollows, ct);
        response.ShouldBeNoStore();
        return await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }

    /// <summary>A contagem pública (<c>{count}</c>, só o número) de <paramref name="route"/>.</summary>
    public static async Task<int> CountAsync(this EngagementApiClient client, string route, CancellationToken ct)
    {
        using var response = await client.GetAsync(route, ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["count"], $"a contagem não é só {{count}}: {body}");
        return body.GetProperty("count").GetInt32();
    }

    /// <summary>Os slugs de <c>cities</c> do corpo de <c>/me/follows</c>.</summary>
    public static IReadOnlyList<string> CitySlugs(this JsonElement follows) =>
        [.. follows.GetProperty("cities").EnumerateArray().Select(city => city.GetProperty("slug").GetString() ?? string.Empty)];

    /// <summary>Os ids de <c>profiles</c> do corpo de <c>/me/follows</c>.</summary>
    public static IReadOnlyList<Guid> ProfileIds(this JsonElement follows) =>
        [.. follows.GetProperty("profiles").EnumerateArray().Select(profile => profile.GetProperty("id").GetGuid())];

    /// <summary>O admin cria um perfil aprovado sem dono (<c>POST /admin/profiles</c>, 201); devolve o id.</summary>
    public static async Task<Guid> CreateApprovedProfileAsync(
        this EngagementApiClient admin, string name, CancellationToken ct, string type = "band")
    {
        using var response = await admin.PostAsync(
            AdminProfiles,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["name"] = name,
                ["city"] = EngagementApiFactory.City,
                ["styles"] = type == "band" ? new[] { EngagementApiFactory.Style } : Array.Empty<string>(),
                ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
            },
            ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return created.GetProperty("profileId").GetGuid();
    }

    /// <summary>O fã pede um perfil (fica pendente, SPEC-0021); devolve o id do pedido.</summary>
    public static async Task<Guid> RequestProfileAsync(this EngagementApiClient fan, string name, CancellationToken ct)
    {
        using var response = await fan.PostAsync(
            ProfileRequests,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "band",
                ["name"] = name,
                ["city"] = EngagementApiFactory.City,
                ["styles"] = new[] { EngagementApiFactory.Style },
                ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
                ["contact"] = "@banda.pendente no Instagram",
            },
            ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return created.GetProperty("requestId").GetGuid();
    }
}
