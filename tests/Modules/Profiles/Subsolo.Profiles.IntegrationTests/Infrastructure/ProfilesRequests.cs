using System.Net;
using System.Text.Json;

namespace Subsolo.Profiles.IntegrationTests.Infrastructure;

/// <summary>SPEC-0021 — as rotas do §6 e os corpos de pedido usados pelos testes.</summary>
internal static class ProfilesRequests
{
    public const string Requests = "/api/v1/profiles/requests";

    public const string Search = "/api/v1/profiles/search";

    public const string MyProfiles = "/api/v1/me/profiles";

    public const string AdminQueue = "/api/v1/admin/profile-requests";

    public const string AdminProfiles = "/api/v1/admin/profiles";

    public const string Export = "/api/v1/me/export";

    public const string Me = "/api/v1/me";

    public static string Claims(Guid profileId) => $"/api/v1/profiles/{profileId}/claims";

    public static string Approve(Guid requestId) => $"{AdminQueue}/{requestId}/approve";

    public static string Reject(Guid requestId) => $"{AdminQueue}/{requestId}/reject";

    public static string SearchFor(string q, string? type = null) =>
        type is null ? $"{Search}?q={Uri.EscapeDataString(q)}" : $"{Search}?q={Uri.EscapeDataString(q)}&type={type}";

    /// <summary>Corpo de um pedido de banda válido (cidade e estilo semeados em Development).</summary>
    public static Dictionary<string, object?> Band(string name, string contact = "@podridao no Instagram") => new(StringComparer.Ordinal)
    {
        ["type"] = "band",
        ["name"] = name,
        ["city"] = ProfilesApiFactory.City,
        ["styles"] = new[] { ProfilesApiFactory.Style },
        ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal) { ["instagram"] = "https://www.instagram.com/podridao" },
        ["contact"] = contact,
    };

    /// <summary>Corpo de um perfil criado pelo admin (sem contato).</summary>
    public static Dictionary<string, object?> Ownerless(string name, string type = "venue")
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["name"] = name,
            ["city"] = ProfilesApiFactory.City,
            ["styles"] = type == "band" ? new[] { ProfilesApiFactory.Style } : Array.Empty<string>(),
            ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
        };
        return body;
    }

    /// <summary><c>POST /profiles/requests</c> com 201; devolve o <c>requestId</c>.</summary>
    public static async Task<Guid> RequestProfileAsync(this ProfilesApiClient client, object body, CancellationToken ct)
    {
        using var response = await client.PostAsync(Requests, body, ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        created.GetProperty("status").GetString().ShouldBe("pending");
        return created.GetProperty("requestId").GetGuid();
    }

    /// <summary><c>POST /profiles/{id}/claims</c> com 201; devolve o <c>requestId</c>.</summary>
    public static async Task<Guid> ClaimAsync(this ProfilesApiClient client, Guid profileId, string proof, CancellationToken ct)
    {
        using var response = await client.PostAsync(Claims(profileId), new { proof }, ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        created.GetProperty("status").GetString().ShouldBe("pending");
        return created.GetProperty("requestId").GetGuid();
    }

    /// <summary><c>POST /admin/profile-requests/{id}/approve</c> com 200; devolve o <c>profileId</c>.</summary>
    public static async Task<Guid> ApproveAsync(this ProfilesApiClient admin, Guid requestId, CancellationToken ct)
    {
        using var response = await admin.PostAsync(Approve(requestId), null, ct);
        var approved = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return approved.GetProperty("profileId").GetGuid();
    }

    /// <summary><c>POST /admin/profile-requests/{id}/reject</c> com o motivo; exige 200.</summary>
    public static async Task RejectAsync(this ProfilesApiClient admin, Guid requestId, string reason, CancellationToken ct)
    {
        using var response = await admin.PostAsync(Reject(requestId), new { reason }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"POST {Reject(requestId)}: {ProfilesApiClient.Excerpt(body)}");
    }

    /// <summary><c>POST /admin/profiles</c> com 201; devolve o <c>profileId</c>.</summary>
    public static async Task<Guid> CreateOwnerlessAsync(this ProfilesApiClient admin, object body, CancellationToken ct)
    {
        using var response = await admin.PostAsync(AdminProfiles, body, ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return created.GetProperty("profileId").GetGuid();
    }

    /// <summary><c>GET /me/profiles</c> com 200 e <c>no-store</c>; devolve o corpo.</summary>
    public static async Task<JsonElement> MyProfilesAsync(this ProfilesApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(MyProfiles, ct);
        response.ShouldBeNoStore();
        return await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }

    /// <summary><c>GET /admin/profile-requests</c> com 200 e <c>no-store</c>; devolve a lista.</summary>
    public static async Task<JsonElement> QueueAsync(this ProfilesApiClient admin, CancellationToken ct)
    {
        using var response = await admin.GetAsync(AdminQueue, ct);
        response.ShouldBeNoStore();
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.ValueKind.ShouldBe(JsonValueKind.Array, body.ToString());
        return body;
    }

    /// <summary><c>GET /profiles/search</c> com 200; devolve a lista.</summary>
    public static async Task<JsonElement> SearchAsync(this ProfilesApiClient client, string q, CancellationToken ct, string? type = null)
    {
        using var response = await client.GetAsync(SearchFor(q, type), ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.ValueKind.ShouldBe(JsonValueKind.Array, body.ToString());
        return body;
    }

    /// <summary>O item de <paramref name="list"/> com <c>{property} = id</c>, ou nulo.</summary>
    public static JsonElement? ById(this JsonElement list, string property, Guid id) =>
        list.EnumerateArray()
            .Where(item => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetGuid() == id)
            .Select(item => (JsonElement?)item)
            .FirstOrDefault();

    /// <summary>Os papéis de <c>GET /api/v1/me</c>.</summary>
    public static async Task<IReadOnlyList<string>> RolesAsync(this ProfilesApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Me, ct);
        var me = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return [.. me.GetProperty("roles").EnumerateArray().Select(role => role.GetString() ?? string.Empty)];
    }
}
