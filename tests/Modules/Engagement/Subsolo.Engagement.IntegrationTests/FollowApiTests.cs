using System.Net;
using System.Text.Json;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0027 IT-01 — no Subsolo.Api em memória com o PostgreSQL real, dada a cidade semeada <c>cidade-exemplo</c> e um
/// perfil de banda aprovado (criado pelo admin): o fã segue a cidade e o perfil (204), <c>GET /me/follows</c> traz os dois
/// com nome e endereço (<c>{cities: [{slug, name}], profiles: [{id, type, name, slug}]}</c>, <c>no-store</c>) e as
/// contagens públicas (<c>{count}</c>, sem login, só o número) sobem; deixar de seguir (204, também quando não segue) tira
/// da lista e as contagens descem. Cidade inexistente, perfil pendente ou inexistente → 404
/// <c>engagement.target_not_found</c> (no PUT e na contagem).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FollowApiTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.segue@subsolo.test";

    private async Task<EngagementApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task FollowListCountAndUnfollowReflectEachStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        var city = EngagementApiFactory.City;

        // Antes: nada seguido, contagens zeradas.
        var empty = await fan.MyFollowsAsync(ct);
        empty.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["cities", "profiles"], Case.Sensitive, $"o corpo de /me/follows não é o do §6: {empty}");
        empty.CitySlugs().ShouldBeEmpty();
        empty.ProfileIds().ShouldBeEmpty();
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(city), ct)).ShouldBe(0);
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(0);

        // Segue a cidade e o perfil.
        await fan.PutAsync(EngagementRequests.FollowCity(city), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);

        var follows = await fan.MyFollowsAsync(ct);
        var followedCity = follows.GetProperty("cities").EnumerateArray().ShouldHaveSingleItem($"{follows}");
        followedCity.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["name", "slug"], Case.Sensitive, $"cidade fora do §6: {followedCity}");
        followedCity.GetProperty("slug").GetString().ShouldBe(city);
        followedCity.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        var followedBand = follows.GetProperty("profiles").EnumerateArray().ShouldHaveSingleItem($"{follows}");
        followedBand.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["id", "name", "slug", "type"], Case.Sensitive, $"perfil fora do §6: {followedBand}");
        followedBand.GetProperty("id").GetGuid().ShouldBe(bandId);
        followedBand.GetProperty("type").GetString().ShouldBe("band");
        followedBand.GetProperty("name").GetString().ShouldBe("Podridão");
        followedBand.GetProperty("slug").GetString().ShouldBe("podridao");
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(city), ct)).ShouldBe(1);
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(1);

        // Deixa de seguir o perfil: some da lista e a contagem desce; a cidade continua.
        await fan.DeleteAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        var afterProfile = await fan.MyFollowsAsync(ct);
        afterProfile.ProfileIds().ShouldBeEmpty("o perfil continua na lista depois de deixar de seguir");
        afterProfile.CitySlugs().ShouldBe([city]);
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(0);
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(city), ct)).ShouldBe(1);

        // Deixa de seguir a cidade.
        await fan.DeleteAsync(EngagementRequests.FollowCity(city), ct).ShouldSucceedAsync(ct);
        (await fan.MyFollowsAsync(ct)).CitySlugs().ShouldBeEmpty("a cidade continua na lista depois de deixar de seguir");
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(city), ct)).ShouldBe(0);
        (await EngagementDatabase.FollowsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task FollowingTwiceAndUnfollowingWhatIsNotFollowedAreBoth204()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        using var _ = admin;
        using var __ = fan;
        var bandId = await admin.CreateApprovedProfileAsync("Coágulo", ct);

        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, fanId, ct)).Count.ShouldBe(2, "seguir de novo duplicou");

        // DELETE de quem não segue: 204, nada muda.
        await fan.DeleteAsync(EngagementRequests.FollowCity("outra-cidade"), ct).ShouldSucceedAsync(ct);
        await fan.DeleteAsync(EngagementRequests.FollowProfile(Guid.CreateVersion7()), ct).ShouldSucceedAsync(ct);
        await fan.DeleteAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await fan.DeleteAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, fanId, ct))
            .ShouldHaveSingleItem().TargetKind.ShouldBe("city");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task EachFanCountsOnceAndSeesOnlyTheirOwnFollows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.segue@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        var venueId = await admin.CreateApprovedProfileAsync("Porão do Rock", ct, type: "venue");

        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(EngagementRequests.FollowProfile(venueId), ct).ShouldSucceedAsync(ct);

        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(2);
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(venueId), ct)).ShouldBe(1);
        (await fan.MyFollowsAsync(ct)).ProfileIds().ShouldBe([bandId], "a lista traz follow de outro fã");
        var otherFollows = await other.MyFollowsAsync(ct);
        otherFollows.ProfileIds().ShouldBe([bandId, venueId], ignoreOrder: true);
        otherFollows.GetProperty("profiles").EnumerateArray()
            .Single(profile => profile.GetProperty("id").GetGuid() == venueId)
            .GetProperty("type").GetString().ShouldBe("venue");

        // A contagem pública é só o número: sem ids nem nomes de quem segue.
        using var count = await anonymous.GetAsync(EngagementRequests.ProfileFollowers(bandId), ct);
        var raw = await count.Content.ReadAsStringAsync(ct);
        raw.ShouldNotContain(otherId.ToString(), Case.Insensitive, "a contagem expõe o id de quem segue");
        raw.ShouldNotContain("outro.fa.segue", Case.Insensitive, "a contagem expõe quem segue");
    }

    [Theory]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("cidade-que-nao-existe")]
    [InlineData("porto-alegre")]
    public async Task UnknownCityIs404(string slug)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = fan;

        using (var follow = await fan.PutAsync(EngagementRequests.FollowCity(slug), ct))
        {
            await follow.ShouldBeProblemAsync(HttpStatusCode.NotFound, "engagement.target_not_found", ct);
        }

        using (var count = await anonymous.GetAsync(EngagementRequests.CityFollowers(slug), ct))
        {
            await count.ShouldBeProblemAsync(HttpStatusCode.NotFound, "engagement.target_not_found", ct);
        }

        (await EngagementDatabase.FollowsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
        (await fan.MyFollowsAsync(ct)).CitySlugs().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task PendingOrUnknownProfileIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        var (requester, _) = await api.SignedInAsync("fa.pede.perfil@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = fan;
        using var __ = requester;
        var pending = await requester.RequestProfileAsync("Banda Pendente", ct);

        foreach (var target in new[] { pending, Guid.CreateVersion7() })
        {
            using (var follow = await fan.PutAsync(EngagementRequests.FollowProfile(target), ct))
            {
                await follow.ShouldBeProblemAsync(HttpStatusCode.NotFound, "engagement.target_not_found", ct);
            }

            using var count = await anonymous.GetAsync(EngagementRequests.ProfileFollowers(target), ct);
            await count.ShouldBeProblemAsync(HttpStatusCode.NotFound, "engagement.target_not_found", ct);
        }

        (await EngagementDatabase.FollowsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheMeFollowsBodyIsAJsonObjectWithBothLists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var _ = fan;

        var follows = await fan.MyFollowsAsync(ct);

        follows.ValueKind.ShouldBe(JsonValueKind.Object);
        follows.GetProperty("cities").ValueKind.ShouldBe(JsonValueKind.Array);
        follows.GetProperty("profiles").ValueKind.ShouldBe(JsonValueKind.Array);
    }
}
