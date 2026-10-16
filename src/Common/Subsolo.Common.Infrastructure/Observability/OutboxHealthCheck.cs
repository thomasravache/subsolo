using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — entrada <see cref="Name"/> do <c>/health</c>: Unhealthy quando o job da outbox não executa há mais
/// de <see cref="MaxSilence"/> (pelo <see cref="TimeProvider"/>) ou ainda não executou; Healthy caso contrário.
/// Runbook: <c>docs/runbooks/outbox-parada.md</c>.
/// </summary>
public sealed class OutboxHealthCheck : IHealthCheck
{
    /// <summary>Nome da entrada no <c>/health</c> (contrato: <c>entries.outbox</c>).</summary>
    public const string Name = "outbox";

    /// <summary>Tempo máximo sem execução do job da outbox para continuar saudável.</summary>
    public static readonly TimeSpan MaxSilence = TimeSpan.FromSeconds(60);

    private readonly OutboxHeartbeat _heartbeat;
    private readonly TimeProvider _clock;

    public OutboxHealthCheck(OutboxHeartbeat heartbeat, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        ArgumentNullException.ThrowIfNull(clock);
        _heartbeat = heartbeat;
        _clock = clock;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_heartbeat.LastExecutionUtc is not { } last)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("o job da outbox ainda não executou"));
        }

        var silence = _clock.GetUtcNow() - last;
        return Task.FromResult(silence > MaxSilence
            ? HealthCheckResult.Unhealthy($"o job da outbox não executa há {silence.TotalSeconds:F0} s")
            : HealthCheckResult.Healthy());
    }
}
