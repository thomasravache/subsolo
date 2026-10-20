using System.Net;
using System.Text.Json;

namespace Subsolo.Site.IntegrationTests.Infrastructure;

/// <summary>SPEC-0028 — as rotas do §6 e os passos usados pelos testes.</summary>
internal static class SiteRequests
{
    public const string Supporters = "/api/v1/supporters";

    public const string AdminSupporters = "/api/v1/admin/supporters";

    public static string AdminSupporter(Guid id) => $"{AdminSupporters}/{id}";

    /// <summary>O corpo do <c>POST</c> do §6: <c>{ name, url }</c> (<c>url</c> pode ir nulo).</summary>
    public static Dictionary<string, object?> Body(string name, string? url) =>
        new(StringComparer.Ordinal) { ["name"] = name, ["url"] = url };

    /// <summary><c>DELETE</c> com o <c>X-CSRF</c>.</summary>
    public static Task<HttpResponseMessage> DeleteAsync(this SiteApiClient client, string route, CancellationToken ct) =>
        client.MutateAsync(HttpMethod.Delete, route, null, ct);

    /// <summary>O admin cadastra o apoiador (201 <c>{id}</c>, só o id); devolve o id.</summary>
    public static async Task<Guid> AddSupporterAsync(this SiteApiClient admin, string name, string? url, CancellationToken ct)
    {
        using var response = await admin.PostAsync(AdminSupporters, Body(name, url), ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        created.EnumerateObject().Select(property => property.Name).ShouldBe(["id"], $"201 não é só {{id}}: {created}");
        return created.GetProperty("id").GetGuid();
    }

    /// <summary><c>GET /api/v1/supporters</c> com 200; devolve os apoiadores na ordem da resposta.</summary>
    public static async Task<IReadOnlyList<SupporterItem>> SupportersAsync(this SiteApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Supporters, ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.ValueKind.ShouldBe(JsonValueKind.Array, $"GET {Supporters} não é uma lista: {body}");
        return [.. body.EnumerateArray().Select(SupporterItem.From)];
    }

    /// <summary>Exige 204 sem corpo.</summary>
    public static async Task ShouldBeNoContentAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {SiteApiClient.Excerpt(body)}");
        body.ShouldBeEmpty();
    }
}

/// <summary>Um item de <c>GET /api/v1/supporters</c>: exatamente <c>{ id, name, url|null }</c>.</summary>
internal sealed record SupporterItem(Guid Id, string Name, string? Url)
{
    public static SupporterItem From(JsonElement item)
    {
        string[] names = [.. item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
        names.ShouldBe(["id", "name", "url"], $"item fora do contrato {{id, name, url}}: {item}");
        var url = item.GetProperty("url");
        return new SupporterItem(
            item.GetProperty("id").GetGuid(),
            item.GetProperty("name").GetString() ?? string.Empty,
            url.ValueKind == JsonValueKind.Null ? null : url.GetString());
    }
}
