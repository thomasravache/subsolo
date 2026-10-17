using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Infrastructure.Observability;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-02 — o health check da outbox fica Unhealthy quando a última execução do job foi há 61 s e Healthy
/// quando foi há 59 s (limite de 60 s, relógio simulado).
/// </summary>
public sealed class OutboxHealthCheckTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Spec", "SPEC-0009:UT-02")]
    [InlineData(61, HealthStatus.Unhealthy)]
    [InlineData(59, HealthStatus.Healthy)]
    public async Task StatusDependsOnTheLastOutboxExecution(int secondsSinceLastExecution, HealthStatus expected)
    {
        var clock = new FakeTimeProvider(Start);
        var heartbeat = new OutboxHeartbeat();
        heartbeat.RecordExecution(clock.GetUtcNow());
        clock.Advance(TimeSpan.FromSeconds(secondsSinceLastExecution));
        var check = new OutboxHealthCheck(heartbeat, clock);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(OutboxHealthCheck.Name, check, HealthStatus.Unhealthy, tags: null),
        };

        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expected, $"última execução da outbox há {secondsSinceLastExecution} s");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-02")]
    public void LimitIsSixtySeconds() => OutboxHealthCheck.MaxSilence.ShouldBe(TimeSpan.FromSeconds(60));
}
