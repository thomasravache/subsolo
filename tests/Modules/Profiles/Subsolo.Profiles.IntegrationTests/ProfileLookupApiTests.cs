using System.Globalization;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0026 H23-08 (revisão da SPEC-0023; Emenda v2, a) — <c>GET /api/v1/profiles/lookup?q=&amp;type=</c> (artista ou
/// admin, rate limit <c>default</c>), a busca do cadastro de show, no Subsolo.Api em memória com o PostgreSQL real:
/// <list type="bullet">
/// <item>anônimo → 401 <c>auth.required</c>; fã sem perfil → 403 <c>auth.forbidden</c>;</item>
/// <item>o artista e o admin acham perfis aprovados de outros donos e sem dono, nunca pedidos pendentes nem recusados, com
/// o corpo <c>[{id, type, name, slug, city: {slug, name}}]</c> (sem dono nem contato), por nome;</item>
/// <item><c>type</c> filtra o tipo; no máximo 20 resultados; <c>q</c> com menos de 2 caracteres (ou ausente) → 400
/// <c>profiles.invalid_query</c>.</item>
/// </list>
/// A recusa do organizador de outro dono para o artista continua coberta pelo IT-03 da SPEC-0023
/// (<c>ShowsAccessApiTests</c>).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProfileLookupApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan RoleDelivery = TimeSpan.FromSeconds(30);

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    private static string Lookup(string q, string? type = null) =>
        type is null
            ? $"/api/v1/profiles/lookup?q={Uri.EscapeDataString(q)}"
            : $"/api/v1/profiles/lookup?q={Uri.EscapeDataString(q)}&type={type}";

    private static Dictionary<string, object?> Venue(string name) => new(StringComparer.Ordinal)
    {
        ["type"] = "venue",
        ["name"] = name,
        ["city"] = ProfilesApiFactory.City,
        ["styles"] = Array.Empty<string>(),
        ["links"] = new Dictionary<string, string?>(StringComparer.Ordinal),
        ["contact"] = "@casa no Instagram",
    };

    [Fact]
    [Trait("Spec", "SPEC-0026:H23-08")]
    [Trait("Kind", "Integration")]
    public async Task ArtistAndAdminFindApprovedProfilesWithOrWithoutOwnerButNeverPendingOrRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (artist, _) = await api.SignedInAsync("artista.lookup@subsolo.test", ct);
        const string otherEmail = "outro.dono.lookup@subsolo.test";
        var (other, otherId) = await api.SignedInAsync(otherEmail, ct);
        var (pending, _) = await api.SignedInAsync("pendente.lookup@subsolo.test", ct);
        var (rejected, _) = await api.SignedInAsync("recusado.lookup@subsolo.test", ct);
        using var _ = admin;
        using var __ = artist;
        using var ___ = other;
        using var ____ = pending;
        using var _____ = rejected;

        await admin.ApproveAsync(await artist.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct), ct);
        var owned = await admin.ApproveAsync(await other.RequestProfileAsync(Venue("Porão do Rock"), ct), ct);
        var ownerless = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão Coletivo"), ct);
        await pending.RequestProfileAsync(Venue("Porão Pendente"), ct);
        await admin.RejectAsync(await rejected.RequestProfileAsync(Venue("Porão Recusado"), ct), "Sem material.", ct);
        (await Timing.EventuallyAsync(async () => (await artist.RolesAsync(ct)).Contains("artist"), RoleDelivery, ct))
            .ShouldBeTrue("o papel artist não chegou depois da aprovação do perfil");

        foreach (var (who, client) in new[] { ("artista", artist), ("admin", admin) })
        {
            using var response = await client.GetAsync(Lookup("Porão", "venue"), ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{who}: GET {Lookup("Porão", "venue")}: {ProfilesApiClient.Excerpt(raw)}");
            using var document = JsonDocument.Parse(raw);
            var items = document.RootElement.EnumerateArray().ToList();
            items.Select(Name).ShouldBe(["Porão Coletivo", "Porão do Rock"], Case.Sensitive, $"{who}: aprovados com e sem dono, por nome, sem pendente nem recusado");
            items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([ownerless, owned], $"{who}: ids");
            foreach (var item in items)
            {
                item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                    .ShouldBe(["city", "id", "name", "slug", "type"], Case.Sensitive, $"{who}: item fora do §6: {item}");
                item.GetProperty("type").GetString().ShouldBe("venue");
                item.GetProperty("city").GetProperty("slug").GetString().ShouldBe(ProfilesApiFactory.City);
                item.GetProperty("city").GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
            }

            items.Single(item => Name(item) == "Porão do Rock").GetProperty("slug").GetString().ShouldBe("porao-do-rock");
            raw.ShouldNotContain(otherId.ToString(), Case.Insensitive, $"{who}: a busca expõe o id do dono");
            raw.ShouldNotContain(otherEmail, Case.Insensitive, $"{who}: a busca expõe o e-mail do dono");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:H23-08")]
    [Trait("Kind", "Integration")]
    public async Task TheTypeFiltersTheLimitIsTwentyAndAShortQueryIsInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = admin;
        await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Lote Casa", "venue"), ct);
        for (var i = 1; i <= 25; i++)
        {
            await admin.CreateOwnerlessAsync(
                ProfilesRequests.Ownerless(string.Create(CultureInfo.InvariantCulture, $"Lote Banda {i:00}"), "band"), ct);
        }

        var all = await LookupAsync(admin, Lookup("Lote"), ct);
        all.Count.ShouldBe(20, "a busca devolve no máximo 20");
        all.Select(Name).ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal, "a busca não vem por nome");

        var venues = await LookupAsync(admin, Lookup("Lote", "venue"), ct);
        venues.Select(Name).ShouldBe(["Lote Casa"], Case.Sensitive, "type=venue trouxe outro tipo");
        (await LookupAsync(admin, Lookup("Lote", "band"), ct)).ShouldAllBe(item => item.GetProperty("type").GetString() == "band");

        foreach (var route in new[] { Lookup("L"), Lookup(" "), "/api/v1/profiles/lookup" })
        {
            using var response = await admin.GetAsync(route, ct);
            await Should.NotThrowAsync(
                () => response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "profiles.invalid_query", ct),
                $"GET {route} deveria dar 400 profiles.invalid_query");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:H23-08")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousIsAskedToSignInAndAFanWithoutProfileIsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.lookup@subsolo.test", ct);
        using var _ = fan;
        using var anonymous = api.CreateProfilesClient();

        using (var response = await anonymous.GetAsync(Lookup("Porão"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var response = await fan.GetAsync(Lookup("Porão"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }
    }

    private static string Name(JsonElement item) => item.GetProperty("name").GetString() ?? string.Empty;

    private static async Task<List<JsonElement>> LookupAsync(ProfilesApiClient client, string route, CancellationToken ct)
    {
        using var response = await client.GetAsync(route, ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.ValueKind.ShouldBe(JsonValueKind.Array, body.ToString());
        return [.. body.EnumerateArray()];
    }
}
