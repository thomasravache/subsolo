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
