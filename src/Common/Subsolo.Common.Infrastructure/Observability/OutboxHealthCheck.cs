using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — entrada <see cref="Name"/> do <c>/health</c>: Unhealthy quando o job da outbox não executa há mais
/// de <see cref="MaxSilence"/> (pelo <see cref="TimeProvider"/>); Healthy caso contrário.
/// </summary>
public sealed class OutboxHealthCheck : IHealthCheck
{
    /// <summary>Nome da entrada no <c>/health</c> (contrato: <c>entries.outbox</c>).</summary>
    public const string Name = "outbox";

    /// <summary>Tempo máximo sem execução do job da outbox para continuar saudável.</summary>
    public static readonly TimeSpan MaxSilence = TimeSpan.FromSeconds(60);

    public OutboxHealthCheck(OutboxHeartbeat heartbeat, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
