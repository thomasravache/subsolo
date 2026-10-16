using System.Collections.Concurrent;
using Quartz;

namespace Subsolo.Common.IntegrationTests.Infrastructure;

/// <summary>
/// Listener do Quartz que registra, por job, o horário agendado (relógio simulado) da última execução concluída.
/// Permite esperar que todas as execuções devidas até um instante tenham terminado, sem espera fixa em tempo real.
/// </summary>
internal sealed class JobExecutionCounter : IJobListener
{
    private readonly ConcurrentDictionary<JobKey, DateTimeOffset> _lastCompleted = new();
    private readonly ConcurrentDictionary<JobKey, int> _completed = new();

    public string Name => nameof(JobExecutionCounter);

    /// <summary>Execuções concluídas do job <paramref name="job"/> (com ou sem exceção).</summary>
    public int Completed(JobKey job) => _completed.GetValueOrDefault(job);

    /// <summary>
    /// Horário agendado da última execução concluída do job, ou <see langword="null"/> se ele ainda não rodou.
    /// </summary>
    public DateTimeOffset? LastCompletedScheduledFire(JobKey job) =>
        _lastCompleted.TryGetValue(job, out var fire) ? fire : null;

    public ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = context.JobDetail.Key;
        var fire = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;
        _lastCompleted.AddOrUpdate(key, fire, (_, previous) => fire > previous ? fire : previous);
        _completed.AddOrUpdate(key, 1, (_, count) => count + 1);
        return ValueTask.CompletedTask;
    }
}
