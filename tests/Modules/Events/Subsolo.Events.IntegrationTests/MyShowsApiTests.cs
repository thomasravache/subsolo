using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 IT-06 — dados shows passados e futuros de dois donos (e um do admin, num perfil sem dono), cada um chama
/// <c>GET /me/shows?period=upcoming</c> e <c>past</c> e vê só os próprios, separados por período, com
/// <c>Cache-Control: no-store</c> e os campos do §6. O admin vê os que criou. Tempo controlado com
/// <see cref="FakeTimeProvider"/>: os "passados" são cadastrados antes de o relógio andar 3 dias.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MyShowsApiTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0023:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task EachOwnerSeesOnlyTheirOwnShowsSplitByPeriodWithNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (a, _, producerA) = await api.ProducerAsync(admin, "dono.a.it06@subsolo.test", "Peste Produções", ct, clock);
        var (b, _, producerB) = await api.ProducerAsync(admin, "dono.b.it06@subsolo.test", "Caos Produções", ct, clock);
        using var __ = a;
        using var ___ = b;
        var ownerless = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerA = await a.FlyerAsync(ct, seed: 61);
        var flyerB = await b.FlyerAsync(ct, seed: 62);

        var pastA = (await a.CreateShowAsync(ShowsApi.ShowBody(producerA, flyerA, ShowsApi.LocalAt(clock.GetUtcNow(), days: 1), title: "A Passado"), ct)).Id;
        var pastB = (await b.CreateShowAsync(ShowsApi.ShowBody(producerB, flyerB, ShowsApi.LocalAt(clock.GetUtcNow(), days: 1), title: "B Passado"), ct)).Id;
        clock.Advance(TimeSpan.FromDays(3));
        var upcomingA = (await a.CreateShowAsync(ShowsApi.ShowBody(producerA, flyerA, ShowsApi.LocalAt(clock.GetUtcNow(), days: 10), title: "A Próximo"), ct)).Id;
        var upcomingB = (await b.CreateShowAsync(ShowsApi.ShowBody(producerB, flyerB, ShowsApi.LocalAt(clock.GetUtcNow(), days: 12), title: "B Próximo"), ct)).Id;
        var upcomingAdmin = (await admin.CreateShowAsync(ShowsApi.ShowBody(ownerless, flyerA, ShowsApi.LocalAt(clock.GetUtcNow(), days: 14), title: "Do Admin"), ct)).Id;

        (await MineAsync(a, "upcoming", ct)).ShouldBe([upcomingA]);
        (await MineAsync(a, "past", ct)).ShouldBe([pastA]);
        (await MineAsync(b, "upcoming", ct)).ShouldBe([upcomingB]);
        (await MineAsync(b, "past", ct)).ShouldBe([pastB]);
        (await MineAsync(admin, "upcoming", ct)).ShouldBe([upcomingAdmin], "o admin vê os shows que criou");

        using var response = await a.GetAsync(ShowsApi.MyShowsFor("upcoming"), ct);
        var item = (await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).EnumerateArray().Single();
        foreach (var field in new[] { "id", "slug", "title", "startsAt", "city", "venueName", "status", "flyerThumbUrl", "organizerProfileId" })
        {
            item.TryGetProperty(field, out var _).ShouldBeTrue($"item de /me/shows sem {field}: {item}");
        }

        item.GetProperty("title").GetString().ShouldBe("A Próximo");
        item.GetProperty("city").GetString().ShouldBe(ShowsApi.City);
        item.GetProperty("venueName").GetString().ShouldBe("Porão do Rock");
        item.GetProperty("status").GetString().ShouldBe("scheduled");
        item.GetProperty("organizerProfileId").GetGuid().ShouldBe(producerA);
        item.GetProperty("flyerThumbUrl").GetString().ShouldBe($"/media/flyers/{flyerA}-thumb.webp");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task AFanCannotListShowsAndAnAnonymousIsAskedToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (fan, _) = await api.SignedInAsync("fa.it06@subsolo.test", ct);
        using var _ = fan;
        using var anonymous = api.CreateFlyersClient();

        using (var response = await fan.GetAsync(ShowsApi.MyShowsFor("upcoming"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        using (var response = await anonymous.GetAsync(ShowsApi.MyShowsFor("upcoming"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }
    }

    /// <summary>
    /// SPEC-0024 H23-06 (revisão da SPEC-0023) — cada item de <c>GET /me/shows</c> traz o <c>timeZone</c> (IANA) da cidade
    /// do show, campo aditivo: a tela "Meus shows" deixa de consultar a agenda pública só para ler o fuso. Um show numa
    /// cidade de Manaus e outro na cidade de exemplo (São Paulo).
    /// </summary>
    [Fact]
    [Trait("Spec", "SPEC-0024:H23-06")]
    [Trait("Kind", "Integration")]
    public async Task EachItemCarriesTheTimeZoneOfItsCity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        string manaus;
        using (var city = await admin.PostJsonAsync(
                   ShowsApi.AdminCities, new { name = "Manaus", state = "AM", timeZone = "America/Manaus", slug = (string?)null }, ct))
        {
            manaus = (await city.ShouldBeJsonAsync(HttpStatusCode.Created, ct)).GetProperty("slug").GetString().ShouldNotBeNull();
        }

        var (owner, _, producer) = await api.ProducerAsync(admin, "dono.h2306@subsolo.test", "Peste Produções", ct);
        using var __ = owner;
        var flyerId = await owner.FlyerAsync(ct, seed: 63);
        var now = DateTimeOffset.UtcNow;
        var inManaus = (await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(now, days: 10), title: "Show em Manaus", city: manaus), ct)).Id;
        var inSaoPaulo = (await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(now, days: 11), title: "Show em São Paulo"), ct)).Id;

        using var response = await owner.GetAsync(ShowsApi.MyShowsFor("upcoming"), ct);
        var list = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);

        foreach (var (id, timeZone) in new[] { (inManaus, "America/Manaus"), (inSaoPaulo, "America/Sao_Paulo") })
        {
            var item = list.ById(id).ShouldNotBeNull($"o show {id} não está em /me/shows: {list}");
            item.TryGetProperty("timeZone", out var zone).ShouldBeTrue($"item de /me/shows sem timeZone: {item}");
            zone.GetString().ShouldBe(timeZone, $"timeZone do item {item}");
        }
    }

    /// <summary><c>GET /me/shows?period=</c> com 200 e <c>no-store</c>; devolve os ids na ordem.</summary>
    private static async Task<IReadOnlyList<Guid>> MineAsync(FlyersApiClient client, string period, CancellationToken ct)
    {
        using var response = await client.GetAsync(ShowsApi.MyShowsFor(period), ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        (response.Headers.CacheControl?.NoStore ?? false).ShouldBeTrue($"GET /me/shows?period={period} sem Cache-Control: no-store");
        body.ValueKind.ShouldBe(JsonValueKind.Array, body.ToString());
        return [.. body.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }
}
