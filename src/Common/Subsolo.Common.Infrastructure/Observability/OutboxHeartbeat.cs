namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — instante da última execução do job da outbox, lido pelo <see cref="OutboxHealthCheck"/>. Singleton:
/// o <see cref="OutboxHeartbeatListener"/> registra cada execução com <see cref="RecordExecution"/>.
/// </summary>
public sealed class OutboxHeartbeat
{
    private long _lastExecutionUtcTicks = long.MinValue;

    /// <summary>Instante (UTC) da última execução registrada, ou <see langword="null"/> se o job ainda não rodou.</summary>
    public DateTimeOffset? LastExecutionUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastExecutionUtcTicks);
            return ticks == long.MinValue ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>Registra uma execução do job da outbox no instante <paramref name="executedAtUtc"/>.</summary>
    public void RecordExecution(DateTimeOffset executedAtUtc) =>
        Interlocked.Exchange(ref _lastExecutionUtcTicks, executedAtUtc.UtcTicks);
}
