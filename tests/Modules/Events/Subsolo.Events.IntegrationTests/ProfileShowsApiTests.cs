using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0026 IT-02 — <c>GET /api/v1/shows?profileId={id}&amp;period=upcoming|past</c> (público, sem login) no
/// Subsolo.Api em memória com o PostgreSQL real, com um perfil de casa em mais de 200 shows entre passados e futuros:
/// <list type="bullet">
/// <item>o perfil entra como organizador, como casa (<c>venue.profileId</c>) e, para a banda, como banda do line-up; o
/// show de outra casa não entra;</item>
/// <item><c>upcoming</c>: até 50, por data crescente; <c>past</c>: até 20, mais recentes primeiro; cada item com o resumo
/// de show da agenda (SPEC-0008);</item>
/// <item>p95 de 20 chamadas ≤ 150 ms em <c>upcoming</c>, em <c>past</c> e no perfil público
/// (<c>GET /api/v1/profiles/venue/{slug}</c>, o outro endpoint da página — Mapa de Comportamentos, "Desempenho");</item>
/// <item><c>period</c> inválido, <c>profileId</c> ausente ou mal formado → 400 <c>events.invalid_query</c>.</item>
/// </list>
/// Tempo controlado com <see cref="FakeTimeProvider"/>: os "passados" são cadastrados antes de o relógio andar 100 dias;
/// quem cadastra é o admin, com perfis sem dono.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProfileShowsApiTests(PostgresFixture postgres)
{
    private const int PastBatch = 80;

    private const int UpcomingBatch = 118;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(150);

    private static readonly string[] SummaryFields =
        ["id", "slug", "title", "startsAt", "venueName", "lineup", "styles", "status", "flyerThumbUrl", "attendingCount"];

    private static string ProfileShows(Guid profileId, string period) => $"{ShowsApi.Shows}?profileId={profileId}&period={period}";

    [Fact]
    [Trait("Spec", "SPEC-0026:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AProfileWithTwoHundredShowsGetsTheRolesLimitsAndOrderOfTheContractWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (before, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = before;
        var admin = before;
        var venue = await admin.OwnerlessAsync("venue", "Porão do Rock", ct);
        var band = await admin.OwnerlessAsync("band", "Podridão", ct);
        var producer = await admin.OwnerlessAsync("producer", "Peste Produções", ct);
        var otherVenue = await admin.OwnerlessAsync("venue", "Bar do Zé", ct);
        var flyerId = await admin.FlyerAsync(ct, seed: 101);

        // Os que vão virar passados: a casa organiza 80; um como casa e um da banda no line-up, os mais recentes.
        for (var i = 0; i < PastBatch; i++)
        {
            await CreateAsync(admin, venue, flyerId, clock, days: i + 1, Title("Passado", i), ct);
        }

        await CreateAsync(admin, producer, flyerId, clock, days: 85, "Casa Passada", ct, venueProfile: venue);
        await CreateAsync(admin, producer, flyerId, clock, days: 90, "Banda Passada", ct, lineupProfile: band);
        clock.Advance(TimeSpan.FromDays(100));

        // A sessão de antes expirou com os 100 dias: o admin entra de novo.
        var (after, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var ___ = after;
        admin = after;

        // Os próximos: organizador, casa e banda do line-up; um de outra casa; e mais 118 organizados pela casa.
        await CreateAsync(admin, venue, flyerId, clock, days: 2, "Como Organizador", ct);
        await CreateAsync(admin, producer, flyerId, clock, days: 3, "Sem o Perfil", ct, venueProfile: otherVenue);
        await CreateAsync(admin, producer, flyerId, clock, days: 4, "Como Casa", ct, venueProfile: venue);
        await CreateAsync(admin, producer, flyerId, clock, days: 6, "Como Banda", ct, lineupProfile: band);
        await CreateAsync(admin, band, flyerId, clock, days: 8, "Banda Organiza", ct);
        for (var i = 0; i < UpcomingBatch; i++)
        {
            await CreateAsync(admin, venue, flyerId, clock, days: i + 10, Title("Próximo", i), ct);
        }

        using var anonymous = api.CreateFlyersClient();

        var upcoming = await ShowsAsync(anonymous, venue, "upcoming", ct);
        upcoming.Count.ShouldBe(50, "upcoming devolve até 50");
        upcoming.Select(StartsAt).ShouldBeInOrder(SortDirection.Ascending, "upcoming fora da ordem por data crescente");
        upcoming.Take(2).Select(Title).ShouldBe(["Como Organizador", "Como Casa"], Case.Sensitive, "a casa como organizadora e como casa do show");
        upcoming.Select(Title).ShouldNotContain("Sem o Perfil", "entrou o show de outra casa");
        foreach (var field in SummaryFields)
        {
            upcoming[0].TryGetProperty(field, out var __).ShouldBeTrue($"item sem {field} (resumo da agenda, SPEC-0008): {upcoming[0]}");
        }

        upcoming[0].GetProperty("venueName").GetString().ShouldBe("Porão do Rock");
        upcoming[0].GetProperty("status").GetString().ShouldBe("scheduled");

        var past = await ShowsAsync(anonymous, venue, "past", ct);
        past.Count.ShouldBe(20, "past devolve até 20");
        past.Select(StartsAt).ShouldBeInOrder(SortDirection.Descending, "past fora da ordem (mais recentes primeiro)");
        past[0].GetProperty("title").GetString().ShouldBe("Casa Passada", "o passado mais recente é o show em que o perfil é a casa");
        past.Skip(1).Select(Title).ShouldBe([.. Enumerable.Range(0, 19).Select(i => Title("Passado", PastBatch - 1 - i))]);

        (await ShowsAsync(anonymous, band, "upcoming", ct)).Select(Title)
            .ShouldBe(["Como Banda", "Banda Organiza"], Case.Sensitive, "a banda como banda do line-up e como organizadora");
        (await ShowsAsync(anonymous, band, "past", ct)).Select(Title).ShouldBe(["Banda Passada"]);

        foreach (var route in new[]
        {
            ProfileShows(venue, "upcoming"),
            ProfileShows(venue, "past"),
            "/api/v1/profiles/venue/porao-do-rock",
        })
        {
            await ShouldBeWithinBudgetAsync(anonymous, route, ct);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AnInvalidPeriodOrProfileIdIs400InvalidQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var anonymous = api.CreateFlyersClient();
        var profileId = Guid.CreateVersion7();

        foreach (var query in new[]
        {
            $"profileId={profileId}&period=amanha",
            "period=upcoming",
            "profileId=nao-e-um-id&period=upcoming",
        })
        {
            using var response = await anonymous.GetAsync($"{ShowsApi.Shows}?{query}", ct);
            await Should.NotThrowAsync(
                () => response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_query", ct),
                $"?{query} deveria dar 400 events.invalid_query");
        }
    }

    private static string Title(string prefix, int index) => string.Create(CultureInfo.InvariantCulture, $"{prefix} {index:000}");

    private static string Title(JsonElement show) => show.GetProperty("title").GetString() ?? string.Empty;

    private static DateTimeOffset StartsAt(JsonElement show) => show.GetProperty("startsAt").GetDateTimeOffset();

    private static async Task CreateAsync(
        FlyersApiClient admin,
        Guid organizer,
        string flyerId,
        FakeTimeProvider clock,
        int days,
        string title,
        CancellationToken ct,
        Guid? venueProfile = null,
        Guid? lineupProfile = null) =>
        await admin.CreateShowAsync(
            ShowsApi.ShowBody(
                organizer,
                flyerId,
                ShowsApi.LocalAt(clock.GetUtcNow(), days),
                title: title,
                venue: venueProfile is { } venue ? ShowsApi.WithProfile(venue) : null,
                lineup: lineupProfile is { } band ? [ShowsApi.WithProfile(band), ShowsApi.Named("Cova Rasa")] : null),
            ct);

    /// <summary><c>GET /shows?profileId=&amp;period=</c> com 200 (JSON); devolve a lista <c>shows</c>.</summary>
    private static async Task<List<JsonElement>> ShowsAsync(FlyersApiClient client, Guid profileId, string period, CancellationToken ct)
    {
        using var response = await client.GetAsync(ProfileShows(profileId, period), ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.TryGetProperty("shows", out var shows).ShouldBeTrue($"corpo sem shows: {body}");
        return [.. shows.EnumerateArray()];
    }

    private static async Task ShouldBeWithinBudgetAsync(FlyersApiClient client, string route, CancellationToken ct)
    {
        // Aquecimento (JIT, pool de conexões): fora da medida.
        using (var warmUp = await client.GetAsync(route, ct))
        {
            warmUp.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {route}: {FlyersApiClient.Excerpt(await warmUp.Content.ReadAsStringAsync(ct))}");
        }

        var durations = new List<TimeSpan>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync(route, ct);
            watch.Stop();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            durations.Add(watch.Elapsed);
        }

        Timing.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            Budget,
            $"p95 de GET {route} acima de {Budget.TotalMilliseconds} ms: {string.Join(", ", durations.Select(d => $"{d.TotalMilliseconds:0} ms"))}");
    }
}
