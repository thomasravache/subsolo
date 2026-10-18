using System.Globalization;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-03 — dado um perfil criado pelo admin sem dono (<c>POST /admin/profiles</c> → 201, aprovado e sem dono)
/// e 1.000 perfis semeados, quando um fã busca "pod" e reivindica, então o perfil aparece na busca
/// (<c>[{id, type, name, city, styles}]</c>, até 20, só aprovados sem dono) com p95 ≤ 200 ms; a reivindicação
/// (<c>POST /profiles/{id}/claims</c> → 201 pendente) aprovada torna o fã dono; um segundo fã reivindicando recebe 409
/// <c>profiles.already_owned</c>, e o perfil some da busca. Perfil inexistente → 404 <c>profiles.not_found</c>; com
/// outro pedido em análise → 409 <c>profiles.request_pending</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ClaimApiTests(PostgresFixture postgres)
{
    private const int SeededProfiles = 1000;
    private const int Searches = 20;
    private const string Proof = "Sou o dono do Porão; o telefone da casa é o do cadastro no Instagram.";

    private static readonly TimeSpan SearchBudget = TimeSpan.FromMilliseconds(200);

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TheOwnerlessProfileIsFoundClaimedAndApprovedAndThenLeavesTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.dono.porao@subsolo.test", ct);
        var (other, _) = await api.SignedInAsync("outro.fa.porao@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var token = await admin.CsrfTokenAsync(ct);
        for (var i = 0; i < SeededProfiles; i++)
        {
            var seed = ProfilesRequests.Ownerless(
                string.Create(CultureInfo.InvariantCulture, $"Banda Semente {i:0000}"), i % 3 == 0 ? "band" : "venue");
            using var created = await admin.SendAsync(HttpMethod.Post, ProfilesRequests.AdminProfiles, ct, seed, token);
            created.StatusCode.ShouldBe(HttpStatusCode.Created, ProfilesApiClient.Excerpt(await created.Content.ReadAsStringAsync(ct)));
        }

        var profileId = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão do Rock Podre"), ct);
        var stored = (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Single(profile => profile.Id == profileId);
        stored.OwnerUserId.ShouldBeNull("o perfil criado pelo admin não tem dono");
        stored.Slug.ShouldBe("porao-do-rock-podre");

        var durations = new List<TimeSpan>();
        JsonElement found = default;
        for (var i = 0; i <= Searches; i++)
        {
            var (response, elapsed) = await Timing.TimedAsync(() => fan.GetAsync(ProfilesRequests.SearchFor("pod"), ct));
            using (response)
            {
                found = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            }

            // A primeira busca aquece o host (JIT, pool de conexões) e fica fora da medida.
            if (i > 0)
            {
                durations.Add(elapsed);
            }
        }

        Timing.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            SearchBudget, $"p95 da busca com {SeededProfiles} perfis (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
        found.GetArrayLength().ShouldBeLessThanOrEqualTo(20);
        var hit = found.ById("id", profileId).ShouldNotBeNull($"o perfil sem dono não apareceu na busca: {found}");
        hit.GetProperty("type").GetString().ShouldBe("venue");
        hit.GetProperty("name").GetString().ShouldBe("Porão do Rock Podre");
        hit.GetProperty("city").GetString().ShouldBe(ProfilesApiFactory.City);
        hit.GetProperty("styles").ValueKind.ShouldBe(JsonValueKind.Array);

        var claimId = await fan.ClaimAsync(profileId, Proof, ct);
        var claim = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).Single(request => request.Id == claimId);
        claim.Kind.ShouldBe("claim");
        claim.Status.ShouldBe("pending");
        claim.ProfileId.ShouldBe(profileId);
        claim.RequesterUserId.ShouldBe(fanId);
        (await fan.MyProfilesAsync(ct)).GetProperty("requests").ById("requestId", claimId).ShouldNotBeNull()
            .GetProperty("kind").GetString().ShouldBe("claim");

        (await admin.ApproveAsync(claimId, ct)).ShouldBe(profileId);

        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Single(profile => profile.Id == profileId)
            .OwnerUserId.ShouldBe(fanId, "a reivindicação aprovada torna o fã dono");
        (await fan.MyProfilesAsync(ct)).GetProperty("profiles").ById("id", profileId).ShouldNotBeNull("o perfil reivindicado aparece em Meus perfis");

        using (var late = await other.PostAsync(ProfilesRequests.Claims(profileId), new { proof = Proof }, ct))
        {
            await late.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.already_owned", ct);
        }

        (await other.SearchAsync("pod", ct)).ById("id", profileId).ShouldBeNull("perfil com dono não aparece na busca");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TheSearchShowsOnlyApprovedOwnerlessProfilesFiltersByTypeAndReturnsAtMost20()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (owner, _) = await api.SignedInAsync("dono.podridao@subsolo.test", ct);
        var (fan, _) = await api.SignedInAsync("fa.busca@subsolo.test", ct);
        using var _ = admin;
        using var __ = owner;
        using var ___ = fan;
        for (var i = 0; i < 25; i++)
        {
            await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless(string.Create(CultureInfo.InvariantCulture, $"Podre {i:00}")), ct);
        }

        var band = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Podreira Sonora", "band"), ct);
        var ownedRequest = await owner.RequestProfileAsync(ProfilesRequests.Band("Podridão Com Dono"), ct);
        var ownedId = await admin.ApproveAsync(ownedRequest, ct);
        await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão Pendente"), ct);

        var all = await fan.SearchAsync("pod", ct);
        all.GetArrayLength().ShouldBe(20, "a busca devolve até 20 resultados");
        all.ById("id", ownedId).ShouldBeNull("perfil com dono não aparece");
        all.EnumerateArray().Select(item => item.GetProperty("name").GetString())
            .ShouldNotContain("Podridão Pendente", "pedido pendente não é perfil reivindicável");

        var bands = await fan.SearchAsync("pod", ct, type: "band");
        bands.ById("id", band).ShouldNotBeNull($"o filtro por tipo não trouxe a banda: {bands}");
        bands.EnumerateArray().ShouldAllBe(item => item.GetProperty("type").GetString() == "band");

        using var anonymous = api.CreateProfilesClient();
        using var unauthenticated = await anonymous.GetAsync(ProfilesRequests.SearchFor("pod"), ct);
        await unauthenticated.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task ClaimingAnUnknownProfileIs404AndWithAnotherPendingRequestIs409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.duas.vezes@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var profileId = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão do Rock"), ct);

        using (var unknown = await fan.PostAsync(ProfilesRequests.Claims(Guid.CreateVersion7()), new { proof = Proof }, ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "profiles.not_found", ct);
        }

        await fan.ClaimAsync(profileId, Proof, ct);

        using (var twice = await fan.PostAsync(ProfilesRequests.Claims(profileId), new { proof = Proof }, ct))
        {
            await twice.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.request_pending", ct);
        }

        using (var shortProof = await admin.PostAsync(ProfilesRequests.Claims(profileId), new { proof = "ab" }, ct))
        {
            var problem = await shortProof.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "profiles.invalid_request", ct);
            problem.ShouldHaveFieldError("proof");
        }

        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).Count.ShouldBe(1);
    }
}
