using System.Diagnostics;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 IT-03 (§5: 200 shows na janela e 5.000 "vou" em ≤ 30 s) — no Subsolo.Api em memória com o PostgreSQL real e
/// o relógio ajustável: um show daqui a 3 dias (22h, Cidade Exemplo) cadastrado pela API e copiado direto em
/// <c>events.shows</c> até 200 shows agendados no mesmo horário; 25 "vou" por show (5.000 fãs diferentes, com e-mail e o
/// lembrete por e-mail ligado). Disparado o job <c>Engagement.DayBeforeReminder</c> às 10h05 da véspera, as 5.000 entregas
/// <c>reminder</c> por e-mail existem em ≤ 30 s, uma por fã e show (200 chaves). O job de entregas fica pausado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DayBeforeReminderPerformanceTests(PostgresFixture postgres)
{
    private const int Shows = 200;
    private const int AttendeesPerShow = 25;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0032:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TwoHundredShowsAndFiveThousandAttendancesWithinThirtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock, ReminderRequests.ManualReminderSettings());
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var template = await admin.CreateReminderShowAsync("Noite em Volume", clock.GetUtcNow(), days: 3, ct);
        List<Guid> shows = [template.Id, .. await ReminderRequests.CloneShowAsync(api.ConnectionString, template.Id, Shows - 1, ct)];

        var fans = new List<Guid>(Shows * AttendeesPerShow);
        foreach (var show in shows)
        {
            var attendees = Enumerable.Range(0, AttendeesPerShow).Select(_ => Guid.CreateVersion7()).ToList();
            await AttendanceDatabase.SeedAsync(api.ConnectionString, show, attendees, ct);
            fans.AddRange(attendees);
        }

        await NoticesDatabase.SeedUsersAsync(api.ConnectionString, fans, id => $"fa-{id:N}@subsolo.test", ct);
        await ReminderRequests.SeedReminderPreferencesAsync(api.ConnectionString, fans, reminderPush: false, reminderEmail: true, ct);
        await AttendanceDatabase.AnalyzeAsync(api.ConnectionString, ct);
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DayBeforeReminder, ct);
        clock.SetUtcNow(ReminderRequests.EveAt(template.StartsLocal, 10, 5));

        var watch = Stopwatch.StartNew();
        await runs.RunOnceAsync(ct);
        var reached = await Timing.EventuallyAsync(
            async () => await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "reminder", "email", ct) >= fans.Count,
            TimeSpan.FromSeconds(90),
            ct);
        var elapsed = watch.Elapsed;

        reached.ShouldBeTrue(
            $"o lembrete não virou {fans.Count} entregas por e-mail (há {await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "reminder", "email", ct)})");
        elapsed.ShouldBeLessThanOrEqualTo(Budget, $"o lembrete de {Shows} shows e {fans.Count} \"vou\" levou {elapsed.TotalSeconds:F1} s");
        var deliveries = (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.Kind == "reminder").ToList();
        deliveries.Select(row => (row.UserId, row.DedupeKey)).ShouldBeUnique("lembrete repetido para o mesmo fã e show");
        deliveries.Select(row => row.UserId).ToHashSet().SetEquals(fans).ShouldBeTrue("as entregas não são exatamente dos 5.000 fãs");
        deliveries.Select(row => row.DedupeKey).Distinct().Count().ShouldBe(Shows, "uma chave de lembrete por show");
    }
}
