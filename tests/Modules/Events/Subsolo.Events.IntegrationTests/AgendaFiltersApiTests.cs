using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0025 — filtros da agenda da cidade (<c>GET /api/v1/cities/{slug}/agenda?from=&amp;to=&amp;styles=</c>, §6):
/// <list type="bullet">
/// <item>CH-01 — sem parâmetros, a agenda (SPEC-0023) traz os shows de 6 h atrás até 90 dias, ordenados (guarda: passa
/// antes da mudança);</item>
/// <item>IT-01 — shows hoje, no domingo, no próximo mês e daqui a 100 dias: sem parâmetros, com a semana e com o mês,
/// cada resposta traz só os do período, com <c>period</c> e <c>total</c> certos;</item>
/// <item>IT-02 — 300 shows com estilos variados: <c>styles=death,grind</c> com um período traz só os com algum desses
/// estilos no período, em p95 ≤ 300 ms; <c>styles=xpto</c> não quebra (estilo inexistente ignorado);</item>
/// <item>IT-03 — <c>from</c> depois de <c>to</c>, 181 dias e data mal formada → 400 <c>agenda.invalid_range</c>;
/// slug de estilo fora do padrão → 400 <c>agenda.invalid_styles</c>.</item>
/// </list>
/// "Hoje" é fixado com <see cref="FakeTimeProvider"/> ao meio-dia local (São Paulo) do dia corrente: assim os shows de
/// hoje à noite ainda estão no futuro e a janela padrão (−6 h a +90 dias) vai, em datas locais, de hoje a hoje + 90.
/// Os shows são cadastrados pelo admin num perfil sem dono de uma cidade nova (Curitiba, America/Sao_Paulo).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AgendaFiltersApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan AgendaBudget = TimeSpan.FromMilliseconds(300);

    [Fact]
    [Trait("Spec", "SPEC-0025:CH-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutParametersTheAgendaListsTheShowsFromSixHoursAgoToNinetyDaysAheadInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, DateTimeOffset.UtcNow.Day, DateTimeOffset.UtcNow.Hour, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now - TimeSpan.FromHours(10));
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        using var setup = await SetupAsync(api, ct);

        // Com o relógio 10 h antes de "agora": os shows de 7 h e de 5 h atrás ainda estão no futuro.
        var sevenHoursAgo = await setup.CreateAsync(now - TimeSpan.FromHours(7), ct);
        var fiveHoursAgo = await setup.CreateAsync(now - TimeSpan.FromHours(5), ct);
        clock.Advance(TimeSpan.FromHours(10));
        var in89Days = await setup.CreateAsync(now + TimeSpan.FromDays(89), ct);
        var tomorrow = await setup.CreateAsync(now + TimeSpan.FromDays(1), ct);
        var in91Days = await setup.CreateAsync(now + TimeSpan.FromDays(91), ct);

        var body = await setup.Admin.AgendaAsync(setup.City, query: null, ct);

        var listed = Ids(body);
        listed.ShouldBe([fiveHoursAgo, tomorrow, in89Days], "a agenda padrão não traz, em ordem, os shows de −6 h a +90 dias");
        listed.ShouldNotContain(sevenHoursAgo);
        listed.ShouldNotContain(in91Days);
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheDefaultTheWeekAndTheMonthBringOnlyTheShowsOfThePeriodWithPeriodAndTotal()
    {
        var ct = TestContext.Current.CancellationToken;
        var today = TodayInSaoPaulo();
        var clock = new FakeTimeProvider(ShowsApi.UtcOf($"{Iso(today)}T12:00"));
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        using var setup = await SetupAsync(api, ct);
        var sunday = today.AddDays(((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7);
        var endOfMonth = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var nextMonth = endOfMonth.AddDays(1);

        var shows = new List<(Guid Id, DateOnly Date)>
        {
            (await setup.CreateLocalAsync(today, "21:00", ct), today),
            (await setup.CreateLocalAsync(sunday, "22:30", ct), sunday),
            (await setup.CreateLocalAsync(nextMonth, "21:00", ct), nextMonth),
            (await setup.CreateLocalAsync(today.AddDays(100), "21:00", ct), today.AddDays(100)),
        };

        IReadOnlyList<Guid> Between(DateOnly from, DateOnly to) =>
            [.. shows.Where(show => show.Date >= from && show.Date <= to).Select(show => show.Id)];

        var standard = await setup.Admin.AgendaAsync(setup.City, query: null, ct);
        ShouldListExactly(standard, Between(today, today.AddDays(90)), "sem parâmetros");
        ShouldHavePeriod(standard, today, today.AddDays(90), "sem parâmetros");
        Ids(standard).ShouldNotContain(shows[3].Id, "show de daqui a 100 dias na agenda padrão");

        var week = await setup.Admin.AgendaAsync(setup.City, $"from={Iso(today)}&to={Iso(sunday)}", ct);
        ShouldListExactly(week, Between(today, sunday), "esta semana");
        ShouldHavePeriod(week, today, sunday, "esta semana");

        var month = await setup.Admin.AgendaAsync(setup.City, $"from={Iso(today)}&to={Iso(endOfMonth)}", ct);
        ShouldListExactly(month, Between(today, endOfMonth), "este mês");
        ShouldHavePeriod(month, today, endOfMonth, "este mês");
        Ids(month).ShouldNotContain(shows[2].Id, "show do próximo mês em \"este mês\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task StylesAndPeriodTogetherBringOnlyTheMatchingShowsWithinBudgetAndUnknownStylesAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var today = TodayInSaoPaulo();
        var clock = new FakeTimeProvider(ShowsApi.UtcOf($"{Iso(today)}T12:00"));
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        using var setup = await SetupAsync(api, ct);
        string[][] palette =
        [
            ["death"], ["black"], ["grind"], ["doom"], ["thrash"], ["black", "grind"], ["doom", "death"], ["black", "thrash"],
        ];
        var shows = new List<(Guid Id, DateOnly Date, string[] Styles)>();
        for (var i = 0; i < 300; i++)
        {
            var date = today.AddDays(1 + (i % 85));
            var styles = palette[i % palette.Length];
            shows.Add((await setup.CreateLocalAsync(date, $"{19 + (i % 4)}:{(i % 2) * 30:00}", ct, styles), date, styles));
        }

        var from = today.AddDays(10);
        var to = today.AddDays(40);
        string[] wanted = ["death", "grind"];
        IReadOnlyList<Guid> InPeriod(Func<string[], bool> styleMatch) =>
            [.. shows.Where(show => show.Date >= from && show.Date <= to && styleMatch(show.Styles)).Select(show => show.Id)];
        var expected = InPeriod(styles => styles.Intersect(wanted, StringComparer.Ordinal).Any());
        expected.Count.ShouldBeGreaterThan(0);
        InPeriod(_ => true).Count.ShouldBeGreaterThan(expected.Count, "preparação sem shows de outros estilos no período");
        var filtered = $"from={Iso(from)}&to={Iso(to)}&styles=death,grind";

        var body = await setup.Admin.AgendaAsync(setup.City, filtered, ct);

        ShouldListExactly(body, expected, "styles=death,grind no período");
        ShouldHavePeriod(body, from, to, "styles=death,grind no período");
        body.GetProperty("shows").EnumerateArray()
            .ShouldAllBe(show => show.GetProperty("styles").EnumerateArray().Any(style => wanted.Contains(style.GetString() ?? string.Empty)));

        var unknownOnly = await setup.Admin.AgendaAsync(setup.City, $"from={Iso(from)}&to={Iso(to)}&styles=xpto", ct);
        ShouldListExactly(unknownOnly, InPeriod(_ => true), "styles=xpto (estilo inexistente ignorado)");

        var withUnknown = await setup.Admin.AgendaAsync(setup.City, $"from={Iso(from)}&to={Iso(to)}&styles=death,xpto", ct);
        ShouldListExactly(withUnknown, InPeriod(styles => styles.Contains("death")), "styles=death,xpto");

        var durations = new List<TimeSpan>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await setup.Admin.GetAsync($"{ShowsApi.Agenda(setup.City)}?{filtered}", ct);
            durations.Add(watch.Elapsed);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var p95 = Timing.Percentile(durations, 95);
        p95.ShouldBeLessThanOrEqualTo(
            AgendaBudget,
            $"p95 da agenda filtrada com 300 shows = {p95.TotalMilliseconds:F0} ms (ms: {string.Join(", ", durations.Select(d => d.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)))})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0025:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task InvalidPeriodsAndStyleSlugsAreRejectedWithTheirCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var client = api.CreateFlyersClient();
        var today = TodayInSaoPaulo();

        (string Query, string Code)[] cases =
        [
            ($"from={Iso(today.AddDays(10))}&to={Iso(today.AddDays(5))}", "agenda.invalid_range"),
            ($"from={Iso(today)}&to={Iso(today.AddDays(181))}", "agenda.invalid_range"),
            ($"from=2026-13-01&to={Iso(today)}", "agenda.invalid_range"),
            ($"from=12/11/2026&to={Iso(today)}", "agenda.invalid_range"),
            ($"from={Iso(today)}&to=amanha", "agenda.invalid_range"),
            ("styles=death,Grind_X", "agenda.invalid_styles"),
            ("styles=%3Cscript%3E", "agenda.invalid_styles"),
            ($"from={Iso(today)}&to={Iso(today.AddDays(7))}&styles=death%20metal", "agenda.invalid_styles"),
        ];

        foreach (var (query, code) in cases)
        {
            using var response = await client.GetAsync($"{ShowsApi.Agenda(ShowsApi.City)}?{query}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"?{query}: {body}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", $"?{query}");
            using var document = JsonDocument.Parse(body);
            document.RootElement.TryGetProperty("code", out var actual).ShouldBeTrue($"?{query}: ProblemDetails sem code: {body}");
            actual.GetString().ShouldBe(code, $"?{query}: {body}");
        }

        // O limite: 180 dias e to = from ainda são aceitos.
        foreach (var query in new[] { $"from={Iso(today)}&to={Iso(today.AddDays(180))}", $"from={Iso(today)}&to={Iso(today)}" })
        {
            using var response = await client.GetAsync($"{ShowsApi.Agenda(ShowsApi.City)}?{query}", ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"?{query}: {await response.Content.ReadAsStringAsync(ct)}");
        }
    }

    private static async Task<AgendaSetup> SetupAsync(FlyersApiFactory api, CancellationToken ct)
    {
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        var city = await admin.CityAsync("Curitiba", "PR", ct);
        var organizer = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct, city: city);
        var flyerId = await admin.FlyerAsync(ct);
        return new AgendaSetup(admin, city, organizer, flyerId);
    }

    private static DateOnly TodayInSaoPaulo() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShowsApi.SaoPaulo).DateTime);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<Guid> Ids(JsonElement agenda) =>
        [.. agenda.GetProperty("shows").EnumerateArray().Select(show => show.GetProperty("id").GetGuid())];

    private static void ShouldListExactly(JsonElement agenda, IReadOnlyList<Guid> expected, string scenario)
    {
        var listed = Ids(agenda);
        listed.ShouldBe(expected, ignoreOrder: true, $"{scenario}: a agenda não traz exatamente os shows do período");
        var starts = agenda.GetProperty("shows").EnumerateArray().Select(show => show.GetProperty("startsAt").GetDateTimeOffset()).ToList();
        starts.ShouldBe([.. starts.Order()], $"{scenario}: a agenda não está ordenada por data");
        agenda.TryGetProperty("total", out var total).ShouldBeTrue($"{scenario}: resposta sem total: {agenda}");
        total.GetInt32().ShouldBe(expected.Count, $"{scenario}: total");
    }

    private static void ShouldHavePeriod(JsonElement agenda, DateOnly from, DateOnly to, string scenario)
    {
        agenda.TryGetProperty("period", out var period).ShouldBeTrue($"{scenario}: resposta sem period: {agenda}");
        period.GetProperty("from").GetString().ShouldBe(Iso(from), $"{scenario}: period.from");
        period.GetProperty("to").GetString().ShouldBe(Iso(to), $"{scenario}: period.to");
    }

    /// <summary>O admin, a cidade nova, o perfil sem dono que organiza e o flyer dos shows.</summary>
    private sealed class AgendaSetup(FlyersApiClient admin, string city, Guid organizer, string flyerId) : IDisposable
    {
        private int _number;

        public FlyersApiClient Admin => admin;

        public string City => city;

        public void Dispose() => admin.Dispose();

        public Task<Guid> CreateAsync(DateTimeOffset startsAtUtc, CancellationToken ct) =>
            CreateStartingAsync(
                TimeZoneInfo.ConvertTime(startsAtUtc, ShowsApi.SaoPaulo).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                styles: null,
                ct);

        public Task<Guid> CreateLocalAsync(DateOnly date, string time, CancellationToken ct, string[]? styles = null) =>
            CreateStartingAsync($"{Iso(date)}T{time}", styles, ct);

        private async Task<Guid> CreateStartingAsync(string startsLocal, string[]? styles, CancellationToken ct)
        {
            _number++;
            var (id, _) = await Admin.CreateShowAsync(
                ShowsApi.ShowBody(organizer, flyerId, startsLocal, title: $"Show {_number:000}", city: City, styles: styles),
                ct);
            return id;
        }
    }
}

/// <summary>SPEC-0025 — <c>GET /cities/{slug}/agenda[?query]</c> com 200; devolve o corpo inteiro.</summary>
internal static class AgendaFiltersApi
{
    public static async Task<JsonElement> AgendaAsync(this FlyersApiClient client, string city, string? query, CancellationToken ct)
    {
        var route = query is null ? ShowsApi.Agenda(city) : $"{ShowsApi.Agenda(city)}?{query}";
        using var response = await client.GetAsync(route, ct);
        return await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }
}
