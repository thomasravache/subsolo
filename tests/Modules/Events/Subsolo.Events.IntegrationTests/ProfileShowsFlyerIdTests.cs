using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0031 H26-03b (item herdado da revisão da SPEC-0026; Emenda 1, contrato v2) — cada show de
/// <c>GET /api/v1/shows?profileId={id}&amp;period=upcoming</c> (os shows do perfil) traz o <c>flyerId</c> do flyer do
/// show (o mesmo id devolvido pelo envio), aditivo: os outros campos do item continuam. A página do perfil monta com ele
/// a imagem Open Graph (<c>{base}/media/flyers/{flyerId}-og.jpg</c>), sem expressão regular sobre <c>flyerThumbUrl</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProfileShowsFlyerIdTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0031:H26-03b")]
    [Trait("Kind", "Integration")]
    public async Task EachProfileShowCarriesTheIdOfItsFlyer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var venue = await admin.OwnerlessAsync("venue", "Porão do Rock", ct);
        var first = await admin.FlyerAsync(ct, seed: 311);
        var second = await admin.FlyerAsync(ct, seed: 312);
        var now = DateTimeOffset.UtcNow;
        var (firstShow, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(venue, first, ShowsApi.LocalAt(now, 3), title: "Noite do Flyer I"), ct);
        var (secondShow, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(venue, second, ShowsApi.LocalAt(now, 4), title: "Noite do Flyer II"), ct);

        using var response = await admin.GetAsync($"{ShowsApi.Shows}?profileId={venue}&period=upcoming", ct);
        var shows = (await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).GetProperty("shows");

        FlyerIdOf(shows, firstShow).ShouldBe(first);
        FlyerIdOf(shows, secondShow).ShouldBe(second);
        foreach (var item in shows.EnumerateArray())
        {
            item.TryGetProperty("flyerThumbUrl", out _).ShouldBeTrue($"o item perdeu flyerThumbUrl: {item}");
            item.TryGetProperty("city", out _).ShouldBeTrue($"o item perdeu city: {item}");
        }
    }

    private static string? FlyerIdOf(JsonElement shows, Guid showId)
    {
        var item = shows.ById(showId).ShouldNotBeNull($"o show {showId} não está nos shows do perfil");
        item.TryGetProperty("flyerId", out var flyerId).ShouldBeTrue($"o item não tem flyerId: {item}");
        return flyerId.ValueKind == JsonValueKind.Null ? null : flyerId.GetString();
    }
}
