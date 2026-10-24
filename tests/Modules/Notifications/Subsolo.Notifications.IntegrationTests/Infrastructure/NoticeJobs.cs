using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0031 — os jobs do Notifications que os testes controlam (copiado do <c>DeliveryRuns</c> do SPEC-0030:IT-08):
/// pausar e disparar uma execução esperando ela terminar.
/// </summary>
internal static class NoticeJobs
{
    /// <summary>O job de entregas (SPEC-0030).</summary>
    public static readonly JobKey Deliveries = new("Notifications.ProcessDeliveries");

    /// <summary>O job de retenção dos avisos (§6 da SPEC-0031, H30-02).</summary>
    public static readonly JobKey Retention = new("Notifications.NoticesRetention");

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    /// <summary>Pausa o job <paramref name="job"/> (exige que ele esteja agendado).</summary>
    public static async Task PauseAsync(NotificationsApiFactory api, JobKey job, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        (await scheduler.GetJobDetail(job, ct)).ShouldNotBeNull($"job {job} não agendado");
        await scheduler.PauseJob(job, ct);
        // Uma execução já começada antes da pausa (o de entregas roda a cada 1 s) termina logo.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
    }

    /// <summary>Quem dispara o job <paramref name="job"/> (exige que ele esteja agendado) e espera cada execução terminar.</summary>
    public static async Task<JobRuns> RunsAsync(NotificationsApiFactory api, JobKey job, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        (await scheduler.GetJobDetail(job, ct)).ShouldNotBeNull($"job {job} não agendado");
        var runs = new JobRuns(scheduler, job);
        scheduler.ListenerManager.AddJobListener(runs, [Matchers.Key(job)]);
        return runs;
    }

    internal sealed class JobRuns(IScheduler scheduler, JobKey job) : IJobListener
    {
        private readonly object _gate = new();
        private readonly ConcurrentDictionary<string, bool> _running = new(StringComparer.Ordinal);
        private TaskCompletionSource<JobExecutionException?> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name { get; } = $"spec0031-{Guid.NewGuid():N}";

        /// <summary>Dispara uma execução e espera o fim; uma exceção do job reprova o teste.</summary>
        public async Task RunOnceAsync(CancellationToken ct)
        {
            (await Timing.EventuallyAsync(() => Task.FromResult(_running.IsEmpty), Budget, ct))
                .ShouldBeTrue($"o job {job} não terminou a execução anterior");
            Task<JobExecutionException?> finished;
            lock (_gate)
            {
                _finished = new TaskCompletionSource<JobExecutionException?>(TaskCreationOptions.RunContinuationsAsynchronously);
                finished = _finished.Task;
            }

            await scheduler.TriggerJob(job, new JobDataMap(), ct);
            var failure = await finished.WaitAsync(Budget, ct);
            failure.ShouldBeNull($"o job {job} falhou: {failure}");
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
                _finished.TrySetResult(jobException);
            }

            return ValueTask.CompletedTask;
        }
    }
}
