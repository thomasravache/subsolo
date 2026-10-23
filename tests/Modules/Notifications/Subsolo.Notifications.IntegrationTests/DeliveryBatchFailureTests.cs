using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Subsolo.Common.Domain;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-08 (Emenda da revisão G4, 2026-10-23; §5 "um aviso nunca sai duas vezes") — um lote de entregas push e
/// e-mail (aviso <c>reminder</c>, os dois canais ligados) processado pelo job de entregas do host, com os canais trocados
/// por dublês que registram cada envio (<see cref="ScriptedChannel"/>) e o diretório de destinatários por um falso:
/// <list type="bullet">
/// <item>o canal lança uma exceção inesperada numa entrega: só ela fica pendente, com <c>attempts</c> + 1 e a próxima
/// tentativa com espera; as outras ficam <c>sent</c>; processar o lote de novo não reenvia nenhuma já enviada;</item>
/// <item>o lote é cancelado (interrupção do job) com uma entrega ainda em envio: as que já saíram ficam gravadas como
/// <c>sent</c> e não saem de novo no lote seguinte.</item>
/// </list>
/// O job periódico fica pausado; cada lote é disparado pelo teste (<see cref="IScheduler.TriggerJob(JobKey, CancellationToken)"/>)
/// e o fim da execução é observado por um <see cref="IJobListener"/>.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class DeliveryBatchFailureTests(MailpitServer mailpit, PostgresFixture postgres)
{
    /// <summary>O job de entregas (<c>ProcessDeliveriesJob.Name</c>, interno da Infrastructure).</summary>
    private static readonly JobKey DeliveriesJob = new("Notifications.ProcessDeliveries");

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private const int Fans = 5;

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task AnUnexpectedChannelExceptionRetriesOnlyThatDeliveryAndNeverResendsTheOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new ChannelScript();
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit, script.Install);
        var fans = await SeedAsync(api, script, ct);
        var faulty = fans[0].Endpoint;
        script.ThrowFor(faulty);
        var runs = await PausedDeliveriesAsync(api, ct);
        await EnqueueReminderAsync(api, fans, ct);

        var before = DateTimeOffset.UtcNow;
        await runs.RunOnceAsync(ct);

        var states = await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct);
        states.Count.ShouldBe(Fans * 2);
        var failed = states.Single(row => row.UserId == fans[0].UserId && row.Channel == "push");
        var others = states.Where(row => row != failed).ToList();
        others.Where(row => row.Status != "sent")
            .Select(row => $"{row.Channel}/{row.UserId}: {row.Status} (attempts {row.Attempts})")
            .ShouldBeEmpty("a exceção numa entrega descartou o resultado das outras do lote");
        others.ShouldAllBe(row => row.Attempts == 1, "uma entrega enviada não tem attempts = 1");
        failed.Status.ShouldBe("pending", "a entrega que lançou a exceção não ficou para nova tentativa");
        failed.Attempts.ShouldBe(1, "a entrega que lançou a exceção não contou a tentativa");
        failed.NextAttemptAtUtc.ShouldBeGreaterThan(before, "a nova tentativa da entrega que falhou não tem espera");
        script.Attempts().Count.ShouldBe(Fans * 2, "o lote não tentou cada entrega uma vez");

        await runs.RunOnceAsync(ct);

        script.Sent().GroupBy(recipient => recipient, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({group.Count()}x)")
            .ShouldBeEmpty("processar o lote de novo reenviou entregas já enviadas");
        (await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct))
            .Where(row => row.UserId != fans[0].UserId || row.Channel != "push")
            .ShouldAllBe(row => row.Status == "sent" && row.Attempts == 1, "uma entrega enviada mudou no lote seguinte");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task ACancelledBatchKeepsWhatWasAlreadySentAndDoesNotResendIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new ChannelScript();
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit, script.Install);
        var fans = await SeedAsync(api, script, ct);
        var hanging = fans[0].Email;
        script.HangOnceFor(hanging);
        var runs = await PausedDeliveriesAsync(api, ct);
        await EnqueueReminderAsync(api, fans, ct);

        var run = await runs.StartAsync(ct);
        (await Timing.EventuallyAsync(
                () => Task.FromResult(script.Sent().Count == (Fans * 2) - 1 && script.IsHanging),
                Budget,
                ct))
            .ShouldBeTrue($"o lote não chegou ao ponto de cancelamento ({script.Sent().Count} enviadas, uma presa: {script.IsHanging})");
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        await scheduler.Interrupt(DeliveriesJob, ct);
        await run.WaitAsync(Budget, ct);

        var states = await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct);
        var interrupted = states.Single(row => row.UserId == fans[0].UserId && row.Channel == "email");
        states.Where(row => row != interrupted && row.Status != "sent")
            .Select(row => $"{row.Channel}/{row.UserId}: {row.Status} (attempts {row.Attempts})")
            .ShouldBeEmpty("o cancelamento do lote descartou o resultado das entregas que já tinham saído");
        interrupted.Status.ShouldNotBe("sent", "a entrega interrompida ficou como enviada");

        await runs.RunOnceAsync(ct);

        script.Sent().GroupBy(recipient => recipient, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({group.Count()}x)")
            .ShouldBeEmpty("o lote seguinte ao cancelamento reenviou entregas já enviadas");
    }

    /// <summary><see cref="Fans"/> fãs com o lembrete por push e por e-mail ligados, um aparelho e um e-mail cada.</summary>
    private static async Task<IReadOnlyList<Fan>> SeedAsync(NotificationsApiFactory api, ChannelScript script, CancellationToken ct)
    {
        api.CreateApiClient().Dispose();
        var fans = Enumerable.Range(0, Fans)
            .Select(i =>
            {
                var id = Guid.CreateVersion7();
                return new Fan(id, $"https://fcm.googleapis.com/fcm/send/it08-{i}-{id:N}", $"fa{i}-{id:N}@subsolo.test");
            })
            .ToList();
        foreach (var fan in fans)
        {
            script.Emails[fan.UserId] = fan.Email;
        }

        await NotificationsDatabase.SeedPreferencesAsync(
            api.ConnectionString,
            fans.Select(fan => (fan.UserId, NewShowPush: false, DailyDigestEmail: false, ReminderPush: true, ReminderEmail: true)),
            ct);
        await NotificationsDatabase.SeedSubscriptionsAsync(
            api.ConnectionString, fans.Select(fan => (fan.UserId, fan.Endpoint, "p256dh-it08", "auth-it08")), ct);
        return fans;
    }

    private static async Task EnqueueReminderAsync(NotificationsApiFactory api, IReadOnlyList<Fan> fans, CancellationToken ct)
    {
        var enqueued = await api.EnqueueAsync(
            [.. fans.Select(fan => fan.UserId)],
            new Notice(
                "reminder",
                $"it08-{Guid.NewGuid():N}",
                "Amanhã: Podridão no Porão do Rock",
                "Sábado, 21h",
                "https://localhost/cidade-exemplo/shows/podridao-no-porao"),
            ct);
        enqueued.IsSuccess.ShouldBeTrue(enqueued.IsFailure ? $"{enqueued.Error.Code} — {enqueued.Error.Description}" : null);
        (await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct)).Count
            .ShouldBe(Fans * 2, "o aviso não criou uma entrega push e uma de e-mail por fã");
    }

    /// <summary>Pausa o job periódico de entregas e devolve quem dispara e acompanha cada execução.</summary>
    private static async Task<DeliveryRuns> PausedDeliveriesAsync(NotificationsApiFactory api, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        (await scheduler.GetJobDetail(DeliveriesJob, ct)).ShouldNotBeNull($"job {DeliveriesJob} não agendado");
        await scheduler.PauseJob(DeliveriesJob, ct);
        var runs = new DeliveryRuns(scheduler);
        scheduler.ListenerManager.AddJobListener(runs, [Matchers.Key(DeliveriesJob)]);
        // Uma execução já começada antes da pausa (o job roda a cada 1 s) termina logo: ainda não há entregas.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        return runs;
    }

    private sealed record Fan(Guid UserId, string Endpoint, string Email);

    /// <summary>Dispara o job de entregas e espera a execução terminar (o fim é avisado pelo Quartz).</summary>
    private sealed class DeliveryRuns(IScheduler scheduler) : IJobListener
    {
        private readonly object _gate = new();
        private TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<string, bool> _running = new(StringComparer.Ordinal);

        public string Name { get; } = $"{nameof(DeliveryBatchFailureTests)}-{Guid.NewGuid():N}";

        public async Task RunOnceAsync(CancellationToken ct) => await (await StartAsync(ct)).WaitAsync(Budget, ct);

        /// <summary>Dispara uma execução; a tarefa devolvida termina com ela.</summary>
        public async Task<Task> StartAsync(CancellationToken ct)
        {
            (await Timing.EventuallyAsync(() => Task.FromResult(_running.IsEmpty), Budget, ct))
                .ShouldBeTrue("o job de entregas não terminou a execução anterior");
            Task finished;
            lock (_gate)
            {
                _finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                finished = _finished.Task;
            }

            await scheduler.TriggerJob(DeliveriesJob, new JobDataMap(), ct);
            return finished;
        }

        public ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            _running[context.FireInstanceId] = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask JobWasExecuted(
            IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            _running.TryRemove(context.FireInstanceId, out _);
            lock (_gate)
            {
                _finished.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// O roteiro dos canais falsos: registra cada envio (endpoint do push ou e-mail), lança uma exceção inesperada
    /// (<see cref="InvalidOperationException"/>, que nenhum canal trata) para os destinatários de <see cref="ThrowFor"/>
    /// e prende a primeira tentativa de <see cref="HangOnceFor"/> até o lote ser cancelado. Também é o diretório de
    /// destinatários (<see cref="IRecipientDirectory"/>) com os e-mails dos fãs.
    /// </summary>
    private sealed class ChannelScript : IRecipientDirectory
    {
        private readonly ConcurrentQueue<string> _attempts = new();
        private readonly ConcurrentQueue<string> _sent = new();
        private readonly ConcurrentDictionary<string, bool> _throwing = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, bool> _hanging = new(StringComparer.Ordinal);
        private int _hangingNow;

        public ConcurrentDictionary<Guid, string> Emails { get; } = new();

        /// <summary>Há um envio preso esperando o cancelamento.</summary>
        public bool IsHanging => Volatile.Read(ref _hangingNow) > 0;

        public void ThrowFor(string recipient) => _throwing[recipient] = true;

        public void HangOnceFor(string recipient) => _hanging[recipient] = true;

        /// <summary>Todo envio tentado (o destinatário), na ordem.</summary>
        public IReadOnlyList<string> Attempts() => [.. _attempts];

        /// <summary>Todo envio aceito (o destinatário), na ordem.</summary>
        public IReadOnlyList<string> Sent() => [.. _sent];

        /// <summary>Troca os dois canais e o diretório de destinatários do host por este roteiro.</summary>
        public void Install(IServiceCollection services)
        {
            services.RemoveAll<INotificationChannel>();
            services.AddSingleton<INotificationChannel>(new ScriptedChannel(NotificationChannel.WebPush, this));
            services.AddSingleton<INotificationChannel>(new ScriptedChannel(NotificationChannel.Email, this));
            services.RemoveAll<IRecipientDirectory>();
            services.AddSingleton<IRecipientDirectory>(this);
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                userIds.Where(Emails.ContainsKey).ToDictionary(id => id, id => Emails[id]));

        public async Task<Result> SendAsync(string recipient, CancellationToken ct)
        {
            _attempts.Enqueue(recipient);
            if (_throwing.ContainsKey(recipient))
            {
                throw new InvalidOperationException("IT-08: falha inesperada do canal");
            }

            if (_hanging.TryRemove(recipient, out _))
            {
                Interlocked.Increment(ref _hangingNow);
                try
                {
                    await Task.Delay(Budget, ct);
                    throw new TimeoutException("IT-08: o lote não foi cancelado");
                }
                finally
                {
                    Interlocked.Decrement(ref _hangingNow);
                }
            }

            _sent.Enqueue(recipient);
            return Result.Success();
        }
    }

    /// <summary>Um canal falso (push ou e-mail) que segue o <see cref="ChannelScript"/>.</summary>
    private sealed class ScriptedChannel(NotificationChannel channel, ChannelScript script) : INotificationChannel
    {
        public NotificationChannel Channel => channel;

        public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken) =>
            script.SendAsync(notification.Recipient, cancellationToken);
    }
}
