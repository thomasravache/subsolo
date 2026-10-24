using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 — o job <c>Engagement.DayBeforeReminder</c> no Subsolo.Api em memória com o PostgreSQL real e o relógio
/// ajustável (<see cref="RunningClock"/>); o cron do lembrete nunca dispara sozinho (o teste dispara o job) e o job de
/// entregas fica pausado (as entregas ficam em <c>notifications.deliveries</c>; o envio é do SPEC-0030):
/// <list type="bullet">
/// <item>IT-01 — um show daqui a 3 dias (22h, Cidade Exemplo) com três fãs que marcaram "vou" (um com o lembrete por
/// e-mail, um por push, um pelos dois) e outro show com os mesmos "vou" que foi cancelado: o job às 10h05 da véspera cria
/// as entregas <c>reminder</c> dos três fãs, pelos canais que ligaram, com o aviso do §6 ("Amanhã: Cova Rasa",
/// "22h00 · Porão do Rock, Cidade Exemplo", "/cidade-exemplo/{slug}", chave "reminder:{id}:{início UTC}"), e nenhuma do
/// cancelado;</item>
/// <item>IT-02 — o job roda às 10h05 e às 11h05 da véspera; o show é adiado uma semana (o job roda de novo na véspera
/// antiga) e o job roda às 10h05 da nova véspera: cada fã tem uma entrega com a chave da primeira data e uma com a da
/// nova, nenhuma a mais.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DayBeforeReminderJobTests(PostgresFixture postgres)
{
    private const string Reminder = "reminder";

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0032:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AfterTenOnTheEveTheAttendeesGetTheReminderByTheirChannelsAndTheCancelledShowGetsNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock, ReminderRequests.ManualReminderSettings());
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (byEmail, byEmailId) = await api.SignedInAsync($"lembrete.email-{Guid.NewGuid():N}@subsolo.test", ct);
        var (byPush, byPushId) = await api.SignedInAsync($"lembrete.push-{Guid.NewGuid():N}@subsolo.test", ct);
        var (byBoth, byBothId) = await api.SignedInAsync($"lembrete.ambos-{Guid.NewGuid():N}@subsolo.test", ct);
        using var a = admin;
        using var e = byEmail;
        using var p = byPush;
        using var b = byBoth;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var now = clock.GetUtcNow();
        var show = await admin.CreateReminderShowAsync("Noite da Véspera", now, days: 3, ct);
        var cancelled = await admin.CreateReminderShowAsync("Noite Cancelada", now, days: 3, ct);
        foreach (var fan in new[] { byEmail, byPush, byBoth })
        {
            await fan.AttendAsync(show.Id, ct);
            await fan.AttendAsync(cancelled.Id, ct);
        }

        await byEmail.SetReminderPreferencesAsync(reminderPush: false, reminderEmail: true, ct);
        await byPush.SetReminderPreferencesAsync(reminderPush: true, reminderEmail: false, ct);
        await byBoth.SetReminderPreferencesAsync(reminderPush: true, reminderEmail: true, ct);
        await NoticesDatabase.SeedSubscriptionsAsync(api.ConnectionString, [byPushId, byBothId], ct);
        await admin.CancelShowAsync(cancelled.Id, ct);
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DayBeforeReminder, ct);

        clock.SetUtcNow(ReminderRequests.EveAt(show.StartsLocal, 10, 5));
        await runs.RunOnceAsync(ct);

        (string, Guid, string)[] expected =
        [
            (show.Key, byEmailId, "email"),
            (show.Key, byPushId, "push"),
            (show.Key, byBothId, "email"),
            (show.Key, byBothId, "push"),
        ];
        var deliveries = await ReminderDeliveriesAsync(api, expected.Length, ct);
        deliveries.Select(row => (row.DedupeKey, row.UserId, row.Channel)).ShouldBe(
            expected, ignoreOrder: true, "o lembrete não saiu para os três fãs pelos canais que ligaram (ou saiu do cancelado)");
        deliveries.ShouldNotContain(row => row.DedupeKey == cancelled.Key, "o show cancelado gerou lembrete");

        var notice = (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct))
            .Where(row => row.Kind == Reminder)
            .ShouldHaveSingleItem("um aviso de lembrete só (o do show agendado)");
        notice.ShouldBe(new NoticeRow(
            show.Key,
            Reminder,
            "Amanhã: Cova Rasa",
            $"22h00 · {NoticeRequests.Venue}, {NoticeRequests.CityName}",
            $"/{EngagementApiFactory.City}/{show.Slug}"));
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task RunningAtTenAndElevenSendsOnceAndAPostponedShowGetsOneReminderOnTheNewEve()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock, ReminderRequests.ManualReminderSettings());
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var a = admin;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var now = clock.GetUtcNow();
        var show = await admin.CreateReminderShowAsync("Noite Adiada", now, days: 3, ct);
        var fans = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var (fan, fanId) = await api.SignedInAsync($"lembrete.adiado-{index}-{Guid.NewGuid():N}@subsolo.test", ct);
            using var _ = fan;
            await fan.AttendAsync(show.Id, ct);
            await fan.SetReminderPreferencesAsync(reminderPush: false, reminderEmail: true, ct);
            fans.Add(fanId);
        }

        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DayBeforeReminder, ct);

        clock.SetUtcNow(ReminderRequests.EveAt(show.StartsLocal, 10, 5));
        await runs.RunOnceAsync(ct);
        clock.SetUtcNow(ReminderRequests.EveAt(show.StartsLocal, 11, 5));
        await runs.RunOnceAsync(ct);

        var first = await ReminderDeliveriesAsync(api, fans.Count, ct);
        first.Select(row => (row.DedupeKey, row.UserId)).ShouldBe(
            fans.Select(fan => (show.Key, fan)), ignoreOrder: true, "às 10h e às 11h da véspera: um lembrete por fã");

        var postponed = await admin.RescheduleAsync(show, show.StartsLocal.AddDays(7), ct);
        await runs.RunOnceAsync(ct);
        clock.SetUtcNow(ReminderRequests.EveAt(postponed.StartsLocal, 10, 5));
        await runs.RunOnceAsync(ct);

        var all = await ReminderDeliveriesAsync(api, fans.Count * 2, ct);
        all.Select(row => (row.DedupeKey, row.UserId)).ShouldBe(
            fans.Select(fan => (show.Key, fan)).Concat(fans.Select(fan => (postponed.Key, fan))),
            ignoreOrder: true,
            "cada fã: uma entrega na véspera da data antiga e uma na da nova, nenhuma a mais");
    }

    /// <summary>
    /// As entregas <c>reminder</c>, esperando até haver <paramref name="expected"/> (ou o prazo) e mais um instante para uma
    /// entrega a mais aparecer.
    /// </summary>
    private static async Task<IReadOnlyList<NoticeDeliveryRow>> ReminderDeliveriesAsync(
        EngagementApiFactory api, int expected, CancellationToken ct)
    {
        await Timing.EventuallyAsync(
            async () => (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Count(row => row.Kind == Reminder) >= expected,
            Budget,
            ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        return [.. (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.Kind == Reminder)];
    }
}
