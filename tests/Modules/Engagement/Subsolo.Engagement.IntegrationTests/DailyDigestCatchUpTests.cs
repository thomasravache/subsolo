using System.Globalization;
using Quartz;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 H31-01 (item herdado da revisão da SPEC-0031; contrato v2: resumo diário com recuperação) — no Subsolo.Api em
/// memória com o PostgreSQL real e o relógio ajustável, os gatilhos do resumo dirigidos só pela configuração
/// (<c>Engagement:DigestCron</c> e <c>Engagement:DigestCatchUpCron</c>) e lidos por <c>engagement.digest_runs</c>:
/// <list type="bullet">
/// <item>sem linha em <c>digest_runs</c> no dia e com o relógio às 11h de São Paulo, o gatilho de recuperação (a cada 5 s
/// no teste; o regular nunca dispara) roda o resumo com a janela (9h de ontem, 9h de hoje]: entram os shows publicados
/// ontem às 10h e hoje às 8h30, não os de ontem às 8h30 nem de hoje às 10h; grava a linha do dia. Antes das 9h (relógio
/// às 8h) a recuperação não faz nada;</item>
/// <item>as execuções seguintes da recuperação (com a linha do dia gravada) não fazem nada: um <c>DailyDigestCompleted</c>
/// só;</item>
/// <item>o gatilho regular (a cada 5 s no teste; a recuperação nunca dispara) grava a linha do dia em
/// <c>digest_runs</c>.</item>
/// </list>
/// A inbox do Engagement fica pausada (o consumidor do <c>ShowPublished</c> não grava <c>published_shows</c> com outro
/// horário) e o job de entregas também (o aviso fica em <c>notifications.notices</c>).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DailyDigestCatchUpTests(PostgresFixture postgres)
{
    private const string EveryFiveSeconds = "0/5 * * * * ?";

    /// <summary>O <c>Engagement:DigestCron</c> padrão: às 9h de São Paulo.</summary>
    private const string DigestAtNine = "0 0 9 * * ?";

    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutTodaysRunTheCatchUpAtElevenSendsTheWindowFromNineYesterdayToNineTodayOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var today = ReminderRequests.LocalDate(DateTimeOffset.UtcNow);
        var clock = new RunningClock(ReminderRequests.LocalToUtc(today.AddHours(8)));
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            clock,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // SPEC-0036 (H32-02): a API só sobe com um DigestCron que dispara às 9h de São Paulo; o gatilho regular é
                // pausado logo depois da subida (com o relógio ainda às 8h), então só a recuperação roda.
                ["Engagement:DigestCron"] = DigestAtNine,
                ["Engagement:DigestCatchUpCron"] = EveryFiveSeconds,
                ["Engagement:ReminderCron"] = ReminderRequests.NeverCron,
            });
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync($"fa.recuperacao-{Guid.NewGuid():N}@subsolo.test", ct);
        using var a = admin;
        using var f = fan;
        await (await QuartzJobs.SchedulerAsync(api, ct)).PauseTrigger(new TriggerKey(QuartzJobs.DailyDigest.Name), ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.EngagementInbox, ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await fan.SetPreferencesAsync(newShowPush: false, dailyDigestEmail: true, ct);

        var now = clock.GetUtcNow();
        var yesterdayAtTen = await admin.CreateShowAsync("Publicado Ontem às 10h", now, days: 3, ct);
        var todayAtEightThirty = await admin.CreateShowAsync("Publicado Hoje às 8h30", now, days: 4, ct);
        var yesterdayAtEightThirty = await admin.CreateShowAsync("Publicado Ontem às 8h30", now, days: 5, ct);
        var todayAtTen = await admin.CreateShowAsync("Publicado Hoje às 10h", now, days: 6, ct);
        await NoticesDatabase.SeedPublishedShowsAsync(
            api.ConnectionString,
            [
                Published(yesterdayAtTen, today.AddDays(-1).AddHours(10)),
                Published(todayAtEightThirty, today.AddHours(8.5)),
                Published(yesterdayAtEightThirty, today.AddDays(-1).AddHours(8.5)),
                Published(todayAtTen, today.AddHours(10)),
            ],
            ct);
        await Task.Delay(TimeSpan.FromSeconds(6), ct);
        (await ReminderRequests.DigestRunsAsync(api.ConnectionString, ct))
            .ShouldBeEmpty("antes das 9h de São Paulo a recuperação não roda o resumo");

        clock.SetUtcNow(ReminderRequests.LocalToUtc(today.AddHours(11)));

        var ran = await Timing.EventuallyAsync(
            async () => (await ReminderRequests.DigestRunsAsync(api.ConnectionString, ct)).Any(row => row.DigestDate == DateOnly.FromDateTime(today)),
            Arrival,
            ct);
        ran.ShouldBeTrue($"a recuperação às 11h não gravou digest_runs de {today:yyyy-MM-dd}");
        var digest = (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct))
            .Where(notice => notice.Kind == "digest")
            .ShouldHaveSingleItem("a recuperação não enfileirou um resumo só");
        digest.DedupeKey.ShouldBe(string.Create(CultureInfo.InvariantCulture, $"digest:{today:yyyy-MM-dd}"));
        digest.Body.Split('\n').ShouldBe(
            [Line(yesterdayAtTen), Line(todayAtEightThirty)],
            $"a janela da recuperação não é (9h de ontem, 9h de hoje]: {digest.Body}");

        // Mais duas execuções da recuperação (a cada 5 s) com a linha do dia gravada: nada.
        await Task.Delay(TimeSpan.FromSeconds(12), ct);
        api.Logs.Count(log => log.EventId.Name == "DailyDigestCompleted")
            .ShouldBe(1, "a recuperação rodou o resumo de novo com digest_runs do dia gravado");
        (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct)).Count(notice => notice.Kind == "digest")
            .ShouldBe(1, "a recuperação enfileirou outro resumo");
        (await ReminderRequests.DigestRunsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-01")]
    [Trait("Kind", "Integration")]
    public async Task TheRegularTriggerRecordsTodaysRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var today = ReminderRequests.LocalDate(DateTimeOffset.UtcNow);
        var clock = new RunningClock(ReminderRequests.LocalToUtc(today.AddHours(12)));
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            clock,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Engagement:DigestCron"] = EveryFiveSeconds,
                ["Engagement:DigestCatchUpCron"] = ReminderRequests.NeverCron,
                ["Engagement:ReminderCron"] = ReminderRequests.NeverCron,
            });
        api.CreateEngagementClient().Dispose();

        // SPEC-0036:H32-05 — o gatilho regular grava o dia pelo relógio de parede do Quartz: perto da meia-noite de São
        // Paulo, a execução pode cair já no dia seguinte a `today` (calculado antes da subida). Os dois dias valem.
        DateOnly[] accepted = [DateOnly.FromDateTime(today), DateOnly.FromDateTime(today.AddDays(1))];
        var ran = await Timing.EventuallyAsync(
            async () => (await ReminderRequests.DigestRunsAsync(api.ConnectionString, ct)).Any(row => accepted.Contains(row.DigestDate)),
            Arrival,
            ct);

        ran.ShouldBeTrue($"o gatilho regular do resumo não gravou digest_runs de {today:yyyy-MM-dd} (nem do dia seguinte)");
        (await ReminderRequests.DigestRunsAsync(api.ConnectionString, ct))
            .First(row => accepted.Contains(row.DigestDate))
            .CompletedAtUtc.ShouldNotBeNull("digest_runs sem completed_at_utc");
    }

    private static PublishedShowRow Published(CreatedShow show, DateTime publishedLocal) =>
        new(show.Id, EngagementApiFactory.City, [], ReminderRequests.LocalToUtc(publishedLocal));

    private static string Line(CreatedShow show) =>
        $"{show.Day} · Cova Rasa · {NoticeRequests.Venue}, {NoticeRequests.CityName}";
}
