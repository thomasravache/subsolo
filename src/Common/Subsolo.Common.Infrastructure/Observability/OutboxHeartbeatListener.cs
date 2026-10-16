using Microsoft.Extensions.Logging;
using Quartz;

namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — ouve os jobs <c>&lt;Módulo&gt;.ProcessOutbox</c> do Quartz (sem tocar a mensageria): cada execução
/// atualiza o <see cref="OutboxHeartbeat"/> lido pelo <see cref="OutboxHealthCheck"/>; uma execução que termina com
/// exceção gera o evento <c>OutboxProcessingFailed</c>, base do alerta "Falha de job" do Seq
/// (<c>infra/seq/alerts.json</c>, runbook <c>docs/runbooks/outbox-parada.md</c>).
/// </summary>
internal sealed partial class OutboxHeartbeatListener(
    OutboxHeartbeat heartbeat,
    TimeProvider clock,
    ILogger<OutboxHeartbeatListener> logger)
    : IJobListener
{
    /// <summary>Sufixo da identidade dos jobs da outbox (<c>&lt;Módulo&gt;.ProcessOutbox</c>).</summary>
    internal const string JobNameSuffix = ".ProcessOutbox";

    public string Name => nameof(OutboxHeartbeatListener);

    public ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        heartbeat.RecordExecution(clock.GetUtcNow());
        if (jobException is not null)
        {
            LogOutboxProcessingFailed(logger, jobException, context.JobDetail.Key.Name);
        }

        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        EventName = "OutboxProcessingFailed",
        Level = LogLevel.Error,
        Message = "OutboxProcessingFailed: a execução do job {Job} falhou")]
    private static partial void LogOutboxProcessingFailed(ILogger logger, Exception exception, string job);
}
