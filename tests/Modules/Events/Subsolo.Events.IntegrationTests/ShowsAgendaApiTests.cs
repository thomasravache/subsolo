using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 — a agenda da cidade (<c>GET /api/v1/cities/{slug}/agenda</c>, contrato da SPEC-0008):
/// <list type="bullet">
/// <item>CH-01 — sem shows, a resposta segue o contrato da SPEC-0008 com a lista vazia (guarda: passa antes da
/// mudança);</item>
/// <item>IT-05 — com 300 shows em Curitiba (alguns de 7 h atrás e de daqui a 91 dias, fora da janela; alguns de 5 h
/// atrás, dentro) e um show às 22:00 locais, a agenda traz só os de −6 h a +90 dias, ordenados por data, com
/// <c>startsAt</c> em UTC correto e a situação, em p95 ≤ 300 ms. Tempo controlado com <see cref="FakeTimeProvider"/>
/// (os shows "passados" são cadastrados antes de o relógio andar); quem cadastra é o admin, num perfil sem dono.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShowsAgendaApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan AgendaBudget = TimeSpan.FromMilliseconds(300);

    [Fact]
    [Trait("Spec", "SPEC-0023:CH-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutShowsTheAgendaFollowsTheSpec0008ContractWithAnEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateFlyersClient();

        using var response = await client.GetAsync(ShowsApi.Agenda(ShowsApi.City), ct);

        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        var city = body.GetProperty("city");
        city.GetProperty("slug").GetString().ShouldBe(ShowsApi.City);
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        city.GetProperty("state").GetString().ShouldBe("SP");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Sao_Paulo");
        body.GetProperty("shows").ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetProperty("shows").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task TheAgendaListsOnlyTheShowsFromSixHoursAgoToNinetyDaysAheadInOrderWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, DateTimeOffset.UtcNow.Day, DateTimeOffset.UtcNow.Hour, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now - TimeSpan.FromHours(10));
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var curitiba = await admin.CityAsync("Curitiba", "PR", ct);
        var organizer = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct, city: curitiba);
        var flyerId = await admin.FlyerAsync(ct);
        var number = 0;
        var expected = new List<(Guid Id, DateTimeOffset StartsAt)>();
        var outside = new List<Guid>();

        async Task<Guid> CreateAsync(DateTimeOffset startsAtUtc)
        {
            number++;
            var startsLocal = LocalOf(startsAtUtc);
            var (id, _) = await admin.CreateShowAsync(
                ShowsApi.ShowBody(organizer, flyerId, startsLocal, title: $"Show {number:000}", city: curitiba), ct);
            return id;
        }

        // Com o relógio 10 h antes de "agora": shows de 7 h atrás (fora) e de 5 h atrás (dentro) ainda estão no futuro.
        for (var i = 0; i < 5; i++)
        {
            outside.Add(await CreateAsync(now - TimeSpan.FromHours(7) - TimeSpan.FromMinutes(i)));
            var recent = now - TimeSpan.FromHours(5) + TimeSpan.FromMinutes(i);
            expected.Add((await CreateAsync(recent), recent));
        }

        clock.Advance(TimeSpan.FromHours(10));
        for (var i = 0; i < 5; i++)
        {
            outside.Add(await CreateAsync(now + TimeSpan.FromDays(91) + TimeSpan.FromHours(i)));
        }

        for (var i = 0; i < 284; i++)
        {
            var startsAt = now + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(i * 449);
            expected.Add((await CreateAsync(startsAt), startsAt));
        }

        var tenPmLocal = ShowsApi.LocalAt(now, days: 10, hour: 22);
        var tenPm = await CreateAsync(ShowsApi.UtcOf(tenPmLocal));
        expected.Add((tenPm, ShowsApi.UtcOf(tenPmLocal)));
        number.ShouldBe(300);
        using (var postponed = await admin.PostJsonAsync(ShowsApi.Status(tenPm), new { status = "postponed" }, ct))
        {
            postponed.StatusCode.ShouldBe(HttpStatusCode.OK, await postponed.Content.ReadAsStringAsync(ct));
        }

        var shows = await admin.AgendaShowsAsync(curitiba, ct);

        var listed = shows.EnumerateArray().Select(show => show.GetProperty("id").GetGuid()).ToList();
        listed.ShouldBe([.. expected.Select(show => show.Id)], ignoreOrder: true, "a agenda não traz exatamente os shows de −6 h a +90 dias");
        listed.ShouldNotContain(id => outside.Contains(id), "show fora da janela na agenda");
        var starts = shows.EnumerateArray().Select(show => show.GetProperty("startsAt").GetDateTimeOffset()).ToList();
        starts.ShouldBe([.. starts.Order()], "a agenda não está ordenada por data");
        var card = shows.ById(tenPm).ShouldNotBeNull();
        card.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(ShowsApi.UtcOf(tenPmLocal), "22:00 em America/Sao_Paulo em UTC");
        card.GetProperty("startsAt").GetString().ShouldNotBeNull().ShouldEndWith("Z", Case.Sensitive, "startsAt em UTC");
        card.GetProperty("status").GetString().ShouldBe("postponed");
        shows.EnumerateArray().Where(show => show.GetProperty("id").GetGuid() != tenPm)
            .ShouldAllBe(show => show.GetProperty("status").GetString() == "scheduled");

        var durations = new List<TimeSpan>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await admin.GetAsync(ShowsApi.Agenda(curitiba), ct);
            durations.Add(watch.Elapsed);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var p95 = Timing.Percentile(durations, 95);
        p95.ShouldBeLessThanOrEqualTo(
            AgendaBudget,
            $"p95 da agenda com 300 shows = {p95.TotalMilliseconds:F0} ms (ms: {string.Join(", ", durations.Select(d => d.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)))})");
    }

    /// <summary>A hora local (São Paulo) de <paramref name="utc"/> no formato do §6.</summary>
    private static string LocalOf(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, ShowsApi.SaoPaulo).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
