using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0031 — os jobs do Quartz do host que os testes de avisos controlam (copiado do <c>DeliveryRuns</c> do
/// SPEC-0030:IT-08): pausar, retomar e disparar uma execução esperando ela terminar.
/// </summary>
internal static class QuartzJobs
{
    /// <summary>O job do resumo diário (§6, <c>DailyDigest</c> do Engagement).</summary>
    public static readonly JobKey DailyDigest = new("Engagement.DailyDigest");

    /// <summary>SPEC-0032 — o job do lembrete na véspera (§6, <c>DayBeforeReminder</c> do Engagement).</summary>
    public static readonly JobKey DayBeforeReminder = new("Engagement.DayBeforeReminder");

    /// <summary>O job de entregas dos avisos (SPEC-0030).</summary>
    public static readonly JobKey NoticeDeliveries = new("Notifications.ProcessDeliveries");

    /// <summary>A inbox do Engagement (SPEC-0007: <c>{DbContext}.ProcessInbox</c>).</summary>
    public static readonly JobKey EngagementInbox = new("EngagementDbContext.ProcessInbox");

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(120);

    public static async Task<IScheduler> SchedulerAsync(EngagementApiFactory api, CancellationToken ct) =>
        await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);

    /// <summary>Pausa o job <paramref name="job"/> (exige que ele esteja agendado).</summary>
    public static async Task PauseAsync(EngagementApiFactory api, JobKey job, CancellationToken ct)
    {
        var scheduler = await SchedulerAsync(api, ct);
        (await scheduler.GetJobDetail(job, ct)).ShouldNotBeNull($"job {job} não agendado");
        await scheduler.PauseJob(job, ct);
    }

    public static async Task ResumeAsync(EngagementApiFactory api, JobKey job, CancellationToken ct) =>
        await (await SchedulerAsync(api, ct)).ResumeJob(job, ct);

    /// <summary>Quem dispara o job <paramref name="job"/> (exige que ele esteja agendado) e espera cada execução terminar.</summary>
    public static async Task<JobRuns> RunsAsync(EngagementApiFactory api, JobKey job, CancellationToken ct)
    {
        var scheduler = await SchedulerAsync(api, ct);
        (await scheduler.GetJobDetail(job, ct)).ShouldNotBeNull($"job {job} não agendado");
        var runs = new JobRuns(scheduler, job);
        scheduler.ListenerManager.AddJobListener(runs, [Matchers.Key(job)]);
        return runs;
    }

    /// <summary>Dispara o job e espera a execução terminar (o fim é avisado pelo Quartz).</summary>
    internal sealed class JobRuns(IScheduler scheduler, JobKey job) : IJobListener
    {
        private readonly object _gate = new();
        private readonly ConcurrentDictionary<string, bool> _running = new(StringComparer.Ordinal);
        private TaskCompletionSource<JobExecutionException?> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name { get; } = $"spec0031-{Guid.NewGuid():N}";

        /// <summary>Dispara uma execução e espera o fim; uma exceção do job reprova o teste.</summary>
        public async Task RunOnceAsync(CancellationToken ct)
        {
            var failure = await RunAndReportAsync(ct);
            failure.ShouldBeNull($"o job {job} falhou: {failure}");
        }

        /// <summary>Dispara uma execução, espera o fim e devolve a exceção que o job deixou escapar (ou nula).</summary>
        public async Task<JobExecutionException?> RunAndReportAsync(CancellationToken ct)
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
            return await finished.WaitAsync(Budget, ct);
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
