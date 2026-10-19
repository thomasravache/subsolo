using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0026 IT-01 — <c>GET /api/v1/profiles/{type}/{slug}</c> (público, sem login) no Subsolo.Api em memória com o
/// PostgreSQL real: dados um perfil de banda aprovado (pedido do fã + aprovação do admin), um pedido pendente e um
/// recusado, só o aprovado responde 200 com o corpo do §6 (<c>{id, type, name, slug, city: {slug, name}, styles: [{slug,
/// name}], links: {instagram?, bandcamp?, metalArchives?}}</c>), sem dono, contato nem e-mail; os outros, um slug
/// inexistente e o tipo errado na URL (a banda pedida como casa) → 404 <c>profiles.not_found</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PublicProfileApiTests(PostgresFixture postgres)
{
    private const string Instagram = "https://www.instagram.com/podridao";

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    private static string PublicProfile(string type, string slug) => $"/api/v1/profiles/{type}/{slug}";

    [Fact]
    [Trait("Spec", "SPEC-0026:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task OnlyTheApprovedProfileIsPublicWithTheContractBodyAndWithoutOwnerOrContact()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        const string ownerEmail = "dono.publico@subsolo.test";
        const string contact = "@podridao.contato no Instagram";
        var (owner, ownerId) = await api.SignedInAsync(ownerEmail, ct);
        var (pendingFan, _) = await api.SignedInAsync("fa.pendente.publico@subsolo.test", ct);
        var (rejectedFan, _) = await api.SignedInAsync("fa.recusado.publico@subsolo.test", ct);
        using var _ = admin;
        using var __ = owner;
        using var ___ = pendingFan;
        using var ____ = rejectedFan;

        var approvedId = await admin.ApproveAsync(await owner.RequestProfileAsync(ProfilesRequests.Band("Podridão", contact), ct), ct);
        await pendingFan.RequestProfileAsync(ProfilesRequests.Band("Banda Pendente"), ct);
        var rejected = await rejectedFan.RequestProfileAsync(ProfilesRequests.Band("Banda Recusada"), ct);
        await admin.RejectAsync(rejected, "Não achamos material da banda.", ct);

        using var anonymous = api.CreateProfilesClient();
        using var response = await anonymous.GetAsync(PublicProfile("band", "podridao"), ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {PublicProfile("band", "podridao")}: {ProfilesApiClient.Excerpt(raw)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", raw);
        using var document = JsonDocument.Parse(raw);
        var profile = document.RootElement;

        profile.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["city", "id", "links", "name", "slug", "styles", "type"], Case.Sensitive, $"o corpo não é o do §6: {raw}");
        profile.GetProperty("id").GetGuid().ShouldBe(approvedId);
        profile.GetProperty("type").GetString().ShouldBe("band");
        profile.GetProperty("name").GetString().ShouldBe("Podridão");
        profile.GetProperty("slug").GetString().ShouldBe("podridao");
        var city = profile.GetProperty("city");
        city.GetProperty("slug").GetString().ShouldBe(ProfilesApiFactory.City);
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        var style = profile.GetProperty("styles").EnumerateArray().ShouldHaveSingleItem();
        style.GetProperty("slug").GetString().ShouldBe(ProfilesApiFactory.Style);
        style.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace("estilo sem nome");
        var links = profile.GetProperty("links");
        links.GetProperty("instagram").GetString().ShouldBe(Instagram);
        foreach (var absent in new[] { "bandcamp", "metalArchives" })
        {
            (!links.TryGetProperty(absent, out var value) || value.ValueKind == JsonValueKind.Null)
                .ShouldBeTrue($"link {absent} não informado no cadastro, mas veio: {links}");
        }

        // Privacidade (§5): sem dono, contato nem e-mail.
        raw.ShouldNotContain(ownerId.ToString(), Case.Insensitive, "o perfil público expõe o id do dono");
        raw.ShouldNotContain(ownerEmail, Case.Insensitive, "o perfil público expõe o e-mail do dono");
        raw.ShouldNotContain(contact, Case.Insensitive, "o perfil público expõe o contato do pedido");

        foreach (var (type, slug, why) in new[]
        {
            ("band", "banda-pendente", "pedido pendente"),
            ("band", "banda-recusada", "pedido recusado"),
            ("band", "nao-existe", "slug inexistente"),
            ("venue", "podridao", "banda pedida como casa (tipo errado na URL)"),
            ("producer", "podridao", "banda pedida como produtor (tipo errado na URL)"),
        })
        {
            using var notFound = await anonymous.GetAsync(PublicProfile(type, slug), ct);
            await Should.NotThrowAsync(
                () => notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "profiles.not_found", ct),
                $"{why} deveria dar 404 profiles.not_found");
        }
    }
}
