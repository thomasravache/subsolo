using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Engagement.Application.Features.GetAttendees;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0036 IT-01 (H32-04) — no Subsolo.Api em memória com o PostgreSQL real e o relógio ajustável:
/// <list type="bullet">
/// <item>com 200 shows agendados na janela (um cadastrado pela API, daqui a 3 dias às 22h, e copiado direto em
/// <c>events.shows</c>), um "vou" por show (fãs com e-mail e o lembrete por e-mail ligado) e o job
/// <c>Engagement.DayBeforeReminder</c> disparado às 10h05 da véspera, o <see cref="IAttendanceApi"/> do host (contado por
/// um decorador do teste) recebe uma chamada só, em lote (<c>GetAttendeesAsync(IReadOnlyCollection&lt;Guid&gt;)</c>), e
/// nenhuma por show; as 200 entregas <c>reminder</c> existem (uma por fã);</item>
/// <item>depois das migrações do Events, <c>events.shows</c> tem o índice parcial <c>ix_shows_scheduled_starts_at</c>
/// sobre <c>starts_at_utc</c> com <c>status = 'scheduled'</c> (a consulta de hora em hora por situação e início).</item>
/// </list>
/// O job de entregas fica pausado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DayBeforeReminderBatchTests(PostgresFixture postgres)
{
    private const int Shows = 200;

    [Fact]
    [Trait("Spec", "SPEC-0036:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheReminderReadsTheAttendeesOfTwoHundredShowsInOneBatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var calls = new AttendanceApiCalls();
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            clock,
            ReminderRequests.ManualReminderSettings(),
            services: services => services.Replace(ServiceDescriptor.Scoped<IAttendanceApi>(
                provider => new CountingAttendanceApi(ActivatorUtilities.CreateInstance<AttendanceApi>(provider), calls))));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var template = await admin.CreateReminderShowAsync("Noite em Lote", clock.GetUtcNow(), days: 3, ct);
        List<Guid> shows = [template.Id, .. await ReminderRequests.CloneShowAsync(api.ConnectionString, template.Id, Shows - 1, ct)];

        var fans = new List<Guid>(Shows);
        foreach (var show in shows)
        {
            var fan = Guid.CreateVersion7();
            await AttendanceDatabase.SeedAsync(api.ConnectionString, show, [fan], ct);
            fans.Add(fan);
        }

        await NoticesDatabase.SeedUsersAsync(api.ConnectionString, fans, id => $"fa-{id:N}@subsolo.test", ct);
        await ReminderRequests.SeedReminderPreferencesAsync(api.ConnectionString, fans, reminderPush: false, reminderEmail: true, ct);
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DayBeforeReminder, ct);
        clock.SetUtcNow(ReminderRequests.EveAt(template.StartsLocal, 10, 5));
        calls.Reset();

        await runs.RunOnceAsync(ct);

        calls.PerShow.ShouldBe(0, $"o lembrete leu os \"vou\" show a show ({calls.PerShow} chamadas de GetAttendeesAsync(Guid))");
        calls.Batch.ShouldBe(1, "o lembrete não leu os \"vou\" dos 200 shows numa chamada só de GetAttendeesAsync(ids)");
        var reached = await Timing.EventuallyAsync(
            async () => await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "reminder", "email", ct) >= fans.Count,
            TimeSpan.FromSeconds(60),
            ct);
        reached.ShouldBeTrue(
            $"o lembrete em lote não virou {fans.Count} entregas por e-mail (há {await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "reminder", "email", ct)})");
        api.Logs.Where(log => log.EventId.Name == "DayBeforeReminderRunFailed").ShouldBeEmpty("o job do lembrete falhou");
    }

    [Fact]
    [Trait("Spec", "SPEC-0036:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheScheduledShowsHaveTheStartsAtPartialIndexAfterTheMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), settings: ReminderRequests.ManualReminderSettings());
        api.CreateEngagementClient().Dispose();

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select a.attname, pg_get_expr(i.indpred, i.indrelid)
            from pg_index i
            join pg_class ix on ix.oid = i.indexrelid
            join pg_class t on t.oid = i.indrelid
            join pg_namespace n on n.oid = t.relnamespace
            join pg_attribute a on a.attrelid = t.oid and a.attnum = any(i.indkey)
            where n.nspname = 'events' and t.relname = 'shows' and ix.relname = 'ix_shows_scheduled_starts_at'
            """,
            connection);
        var rows = new List<(string Column, string? Predicate)>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
            }
        }

        var index = rows.ShouldHaveSingleItem("events.shows sem o índice ix_shows_scheduled_starts_at(starts_at_utc)");
        index.Column.ShouldBe("starts_at_utc");
        index.Predicate.ShouldNotBeNull("ix_shows_scheduled_starts_at não é parcial (where status = 'scheduled')");
        index.Predicate.ShouldContain("'scheduled'", Case.Sensitive, $"o predicado do índice não é status = 'scheduled': {index.Predicate}");
    }

    /// <summary>Quantas vezes o lembrete chamou cada leitura de quem vai.</summary>
    private sealed class AttendanceApiCalls
    {
        private int _perShow;
        private int _batch;

        public int PerShow => Volatile.Read(ref _perShow);

        public int Batch => Volatile.Read(ref _batch);

        public void CountPerShow() => Interlocked.Increment(ref _perShow);

        public void CountBatch() => Interlocked.Increment(ref _batch);

        public void Reset()
        {
            Interlocked.Exchange(ref _perShow, 0);
            Interlocked.Exchange(ref _batch, 0);
        }
    }

    /// <summary>O <see cref="IAttendanceApi"/> do módulo, contando as leituras de quem vai.</summary>
    private sealed class CountingAttendanceApi(IAttendanceApi inner, AttendanceApiCalls calls) : IAttendanceApi
    {
        public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct) =>
            inner.CountAsync(showIds, ct);

        public Task<IReadOnlyCollection<Guid>> GetAttendeesAsync(Guid showId, CancellationToken ct)
        {
            calls.CountPerShow();
            return inner.GetAttendeesAsync(showId, ct);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetAttendeesAsync(
            IReadOnlyCollection<Guid> showIds, CancellationToken ct)
        {
            calls.CountBatch();
            return inner.GetAttendeesAsync(showIds, ct);
        }
    }
}
