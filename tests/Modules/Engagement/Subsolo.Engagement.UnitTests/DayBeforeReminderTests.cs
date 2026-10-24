using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.DayBeforeReminder;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0032 UT-01 — o seletor de lembretes (<see cref="ReminderSelector"/>) com o relógio fixo, o catálogo e os "vou" em
/// memória e <c>ReminderHour</c> = 10, na véspera de terça 20/10/2026:
/// <list type="bullet">
/// <item>"Cova Rasa + Peste Negra" no Basement, Curitiba (America/Sao_Paulo), quarta 21/10 22h00 locais (01h00 UTC de
/// 22/10), com dois "vou": às 09h55 de Curitiba nada; às 10h05 de Curitiba, o aviso <c>reminder</c> do §6 —
/// "Amanhã: Cova Rasa + Peste Negra", "22h00 · Basement, Curitiba", "/curitiba/{slug}" e a chave
/// "reminder:{id}:202610220100" (início em UTC) — para os dois;</item>
/// <item>"Necrose" no Porão, Manaus (America/Manaus, uma hora atrás), 21/10 20h30 locais: às 10h05 de Curitiba (09h05 em
/// Manaus) ainda nada; às 10h05 de Manaus, o lembrete "20h30 · Porão, Manaus";</item>
/// <item>show de amanhã sem "vou": nada; show de hoje e show de depois de amanhã (com "vou"): nada;</item>
/// <item>show adiado (a data muda de 21/10 para 28/10): nada na véspera antiga; na véspera nova (27/10, 10h05), o lembrete
/// com a chave da nova data;</item>
/// <item>com <c>ReminderHour</c> = 0 (pilha do E2E), a véspera vale desde a meia-noite da cidade.</item>
/// </list>
/// </summary>
public sealed class DayBeforeReminderTests
{
    private const string SaoPaulo = "America/Sao_Paulo";
    private const string Manaus = "America/Manaus";
    private const int ReminderHour = 10;

    private static readonly Guid FanA = Guid.CreateVersion7();
    private static readonly Guid FanB = Guid.CreateVersion7();
    private static readonly Guid FanC = Guid.CreateVersion7();

    /// <summary>Terça 20/10/2026, 09h55 em Curitiba (UTC−3).</summary>
    private static readonly DateTimeOffset BeforeTenInCuritiba = new(2026, 10, 20, 12, 55, 0, TimeSpan.Zero);

    /// <summary>Terça 20/10/2026, 10h05 em Curitiba (09h05 em Manaus).</summary>
    private static readonly DateTimeOffset AfterTenInCuritiba = new(2026, 10, 20, 13, 5, 0, TimeSpan.Zero);

    /// <summary>Terça 20/10/2026, 10h05 em Manaus (UTC−4).</summary>
    private static readonly DateTimeOffset AfterTenInManaus = new(2026, 10, 20, 14, 5, 0, TimeSpan.Zero);

    private sealed record Scenario(EngagementHarness Harness, ShowSummary Curitiba, ShowSummary Manaus, ShowSummary Nobody);

    private static Scenario Arrange()
    {
        var harness = new EngagementHarness();
        var curitiba = Add(harness, "curitiba", "Curitiba", SaoPaulo, ["Cova Rasa", "Peste Negra"], "Basement", Utc(2026, 10, 22, 1, 0));
        var manaus = Add(harness, "manaus", "Manaus", Manaus, ["Necrose"], "Porão", Utc(2026, 10, 22, 0, 30));
        var nobody = Add(harness, "curitiba", "Curitiba", SaoPaulo, ["Ninguém Vai"], "Basement", Utc(2026, 10, 21, 23, 0));
        var today = Add(harness, "curitiba", "Curitiba", SaoPaulo, ["Hoje"], "Basement", Utc(2026, 10, 21, 1, 0));
        var dayAfter = Add(harness, "curitiba", "Curitiba", SaoPaulo, ["Depois de Amanhã"], "Basement", Utc(2026, 10, 22, 12, 0));
        harness.AddAttendance(FanA, curitiba.ShowId);
        harness.AddAttendance(FanB, curitiba.ShowId);
        harness.AddAttendance(FanC, manaus.ShowId);
        harness.AddAttendance(FanA, today.ShowId);
        harness.AddAttendance(FanA, dayAfter.ShowId);
        return new Scenario(harness, curitiba, manaus, nobody);
    }

    private static ShowSummary Add(
        EngagementHarness harness,
        string citySlug,
        string cityName,
        string timeZone,
        IReadOnlyList<string> lineup,
        string venue,
        DateTimeOffset startsAtUtc)
    {
        var id = Guid.CreateVersion7();
        return harness.Catalog.AddSummary(new ShowSummary(
            id, $"show-{id:N}", citySlug, cityName, timeZone, lineup, venue, startsAtUtc, "scheduled"));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static async Task<IReadOnlyList<ReminderNotice>> SelectAtAsync(
        EngagementHarness harness, DateTimeOffset now, int reminderHour = ReminderHour)
    {
        harness.Clock.SetUtcNow(now);
        return await harness.Create<ReminderSelector>().SelectAsync(reminderHour, TestContext.Current.CancellationToken);
    }

    private static string KeyOf(ShowSummary show) =>
        $"reminder:{show.ShowId}:{show.StartsAtUtc.UtcDateTime:yyyyMMddHHmm}";

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task BeforeTenInTheCityNothingIsSelected()
    {
        var scenario = Arrange();

        var notices = await SelectAtAsync(scenario.Harness, BeforeTenInCuritiba);

        notices.ShouldBeEmpty("às 09h55 da véspera ainda não é hora do lembrete");
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task AfterTenInCuritibaTheShowOfTomorrowGetsTheContractNoticeForItsAttendees()
    {
        var scenario = Arrange();

        var notices = await SelectAtAsync(scenario.Harness, AfterTenInCuritiba);

        var reminder = notices.ShouldHaveSingleItem("às 10h05 de Curitiba só o show de amanhã em Curitiba (Manaus ainda são 09h05)");
        reminder.UserIds.ShouldBe([FanA, FanB], ignoreOrder: true);
        reminder.Notice.ShouldBe(new Notice(
            "reminder",
            $"reminder:{scenario.Curitiba.ShowId}:202610220100",
            "Amanhã: Cova Rasa + Peste Negra",
            "22h00 · Basement, Curitiba",
            $"/curitiba/{scenario.Curitiba.Slug}"));
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task ManausUsesTenOClockOfManaus()
    {
        var scenario = Arrange();

        var notices = await SelectAtAsync(scenario.Harness, AfterTenInManaus);

        notices.Select(notice => notice.Notice.DedupeKey)
            .ShouldBe([KeyOf(scenario.Curitiba), KeyOf(scenario.Manaus)], ignoreOrder: true);
        var manaus = notices.Single(notice => notice.Notice.DedupeKey == KeyOf(scenario.Manaus));
        manaus.UserIds.ShouldBe([FanC]);
        manaus.Notice.ShouldBe(new Notice(
            "reminder",
            $"reminder:{scenario.Manaus.ShowId}:202610220030",
            "Amanhã: Necrose",
            "20h30 · Porão, Manaus",
            $"/manaus/{scenario.Manaus.Slug}"));
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task AShowWithoutAttendeesAndShowsOfTodayOrTheDayAfterAreIgnored()
    {
        var scenario = Arrange();

        var notices = await SelectAtAsync(scenario.Harness, AfterTenInManaus);

        notices.Select(notice => notice.Notice.DedupeKey).ShouldNotContain(KeyOf(scenario.Nobody), "show sem \"vou\" gerou lembrete");
        notices.ShouldAllBe(notice => notice.UserIds.Count > 0);
        notices.Select(notice => notice.Notice.Title).ShouldNotContain("Amanhã: Hoje", "o show de hoje não é \"amanhã\"");
        notices.Select(notice => notice.Notice.Title).ShouldNotContain("Amanhã: Depois de Amanhã", "o show de depois de amanhã não é \"amanhã\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task APostponedShowGetsTheReminderOnTheEveOfTheNewDateWithTheNewKey()
    {
        var harness = new EngagementHarness();
        var original = Add(harness, "curitiba", "Curitiba", SaoPaulo, ["Adiada"], "Basement", Utc(2026, 10, 22, 2, 0));
        harness.AddAttendance(FanB, original.ShowId);
        var postponed = harness.Catalog.AddSummary(original with { StartsAtUtc = Utc(2026, 10, 29, 2, 0) });

        var onTheOldEve = await SelectAtAsync(harness, AfterTenInCuritiba);
        var onTheNewEve = await SelectAtAsync(harness, new DateTimeOffset(2026, 10, 27, 13, 5, 0, TimeSpan.Zero));

        onTheOldEve.ShouldBeEmpty("o show adiado ainda gerou lembrete na véspera da data antiga");
        var reminder = onTheNewEve.ShouldHaveSingleItem("o show adiado não gerou lembrete na véspera da nova data");
        reminder.Notice.DedupeKey.ShouldBe($"reminder:{postponed.ShowId}:202610290200");
        reminder.Notice.Body.ShouldBe("23h00 · Basement, Curitiba");
        reminder.UserIds.ShouldBe([FanB]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:UT-01")]
    public async Task WithReminderHourZeroTheEveStartsAtMidnightOfTheCity()
    {
        var scenario = Arrange();

        var notices = await SelectAtAsync(scenario.Harness, new DateTimeOffset(2026, 10, 20, 3, 30, 0, TimeSpan.Zero), reminderHour: 0);

        notices.Select(notice => notice.Notice.DedupeKey).ShouldContain(
            KeyOf(scenario.Curitiba), "com ReminderHour = 0 o lembrete sai já às 00h30 da véspera em Curitiba");
    }
}
