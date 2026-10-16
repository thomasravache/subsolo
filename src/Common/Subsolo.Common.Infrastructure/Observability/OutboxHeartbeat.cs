namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — instante da última execução do job da outbox, lido pelo <see cref="OutboxHealthCheck"/>. Singleton:
/// o job registra cada execução com <see cref="RecordExecution"/>.
/// </summary>
public sealed class OutboxHeartbeat
{
    /// <summary>Instante (UTC) da última execução registrada, ou <see langword="null"/> se o job ainda não rodou.</summary>
    public DateTimeOffset? LastExecutionUtc => throw new NotImplementedException();

    /// <summary>Registra uma execução do job da outbox no instante <paramref name="executedAtUtc"/>.</summary>
    public void RecordExecution(DateTimeOffset executedAtUtc) => throw new NotImplementedException();
}
