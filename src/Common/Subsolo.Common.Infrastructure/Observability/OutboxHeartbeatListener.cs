using Microsoft.Extensions.Logging;
using Quartz;

namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — ouve os jobs <c>&lt;Módulo&gt;.ProcessOutbox</c> e <c>&lt;Módulo&gt;.ProcessInbox</c> do Quartz: cada
/// execução da outbox atualiza o <see cref="OutboxHeartbeat"/> lido pelo <see cref="OutboxHealthCheck"/>; uma
/// execução de qualquer dos dois que termina com exceção (o job inteiro falhou) gera o evento
/// <c>OutboxProcessingFailed</c>, base do alerta "Falha de job" do Seq (<c>infra/seq/alerts.json</c>, runbook
/// <c>docs/runbooks/outbox-parada.md</c>). A falha de uma mensagem gera o mesmo evento no próprio job.
/// </summary>
internal sealed partial class OutboxHeartbeatListener(
    OutboxHeartbeat heartbeat,
    TimeProvider clock,
    ILogger<OutboxHeartbeatListener> logger)
    : IJobListener
{
    /// <summary>Sufixo da identidade dos jobs da outbox (<c>&lt;Módulo&gt;.ProcessOutbox</c>).</summary>
    internal const string OutboxJobSuffix = ".ProcessOutbox";

    /// <summary>Sufixo da identidade dos jobs da inbox (<c>&lt;Módulo&gt;.ProcessInbox</c>).</summary>
    internal const string InboxJobSuffix = ".ProcessInbox";

    public string Name => nameof(OutboxHeartbeatListener);

    public ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.JobDetail.Key.Name.EndsWith(OutboxJobSuffix, StringComparison.Ordinal))
        {
            heartbeat.RecordExecution(clock.GetUtcNow());
        }

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
