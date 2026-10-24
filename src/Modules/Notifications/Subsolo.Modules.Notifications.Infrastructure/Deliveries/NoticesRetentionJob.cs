using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using Subsolo.Modules.Notifications.Domain.Notices;
using Subsolo.Modules.Notifications.Infrastructure.Persistence;

namespace Subsolo.Modules.Notifications.Infrastructure.Deliveries;

/// <summary>
/// SPEC-0031 (H30-02, ADR-0008) — job Quartz <see cref="Name"/>, diário: apaga as entregas concluídas (<c>sent</c>,
/// <c>failed</c>, <c>skipped</c>) e os avisos sem entrega pendente com mais de <see cref="Retention"/>, e registra
/// <c>NoticesRetentionCompleted</c> com as contagens. Uma entrega <c>pending</c> antiga fica (e o aviso dela também): ainda
/// vai sair. Execuções perdidas não são recuperadas (a próxima já faz a limpeza inteira).
/// </summary>
[DisallowConcurrentExecution]
internal sealed partial class NoticesRetentionJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<NoticesRetentionJob> logger) : IJob
{
    /// <summary>Identidade do job e do gatilho no Quartz.</summary>
    public const string Name = "Notifications.NoticesRetention";

    /// <summary>Por quanto tempo as entregas concluídas e os avisos ficam guardados.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    /// <summary>Intervalo entre duas limpezas.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>Agenda o job a cada <see cref="Interval"/>, a primeira vez uma hora depois da subida.</summary>
    public static void Schedule(IQuartzBuilder quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        quartz.ScheduleJob<NoticesRetentionJob>(
            trigger => trigger
                .WithIdentity(Name)
                .StartAt(DateTimeOffset.UtcNow + TimeSpan.FromHours(1))
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(Interval)
                    .RepeatForever()
                    .WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount)),
            job => job.WithIdentity(Name));
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var cutoff = clock.GetUtcNow() - Retention;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var deliveries = new NpgsqlCommand(
            """
            delete from notifications.deliveries
            where status = any(@finished) and created_at_utc < @cutoff
            """,
            connection,
            transaction);
        deliveries.Parameters.Add(new NpgsqlParameter<string[]>(
            "finished", [DeliveryStatuses.Sent, DeliveryStatuses.Failed, DeliveryStatuses.Skipped]));
        deliveries.Parameters.AddWithValue("cutoff", cutoff);
        var deletedDeliveries = await deliveries.ExecuteNonQueryAsync(cancellationToken);

        await using var notices = new NpgsqlCommand(
            """
            delete from notifications.notices n
            where n.created_at_utc < @cutoff
              and not exists (
                  select 1 from notifications.deliveries d where d.notice_id = n.id and d.status = @pending)
            """,
            connection,
            transaction);
        notices.Parameters.AddWithValue("cutoff", cutoff);
        notices.Parameters.AddWithValue("pending", DeliveryStatuses.Pending);
        var deletedNotices = await notices.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        LogCompleted(logger, deletedDeliveries, deletedNotices);
    }

    [LoggerMessage(
        EventName = "NoticesRetentionCompleted",
        Level = LogLevel.Information,
        Message = "Retenção dos avisos: {Deliveries} entrega(s) concluída(s) e {Notices} aviso(s) com mais de 90 dias apagados")]
    private static partial void LogCompleted(ILogger logger, int deliveries, int notices);
}
