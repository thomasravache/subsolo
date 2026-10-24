using Microsoft.Extensions.Logging;
using Npgsql;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0032 H31-04 (item herdado da revisão da SPEC-0031; contrato v2) — com o host no ar e o banco dele fora do ar (o
/// database passa a recusar conexões e as abertas são derrubadas), o job <c>Notifications.NoticesRetention</c> não deixa a
/// exceção escapar para o Quartz e registra o evento <c>NoticesRetentionRunFailed</c> com nível Error (a base do alerta
/// "Falha de job"; o filtro do alerta é conferido no <c>JobFailureAlertCoversNoticeDeliveriesTests</c> do
/// Observability.UnitTests, com a mesma tag). O banco volta antes do fim do teste (como o SPEC-0031:IT-07).
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class NoticesRetentionFailureTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0032:H31-04")]
    [Trait("Kind", "Integration")]
    public async Task WithTheDatabaseDownTheRetentionJobLogsNoticesRetentionRunFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var api = new NotificationsApiFactory(connectionString, mailpit);
        api.CreateApiClient().Dispose();
        await NoticeJobs.PauseAsync(api, NoticeJobs.Deliveries, ct);
        var runs = await NoticeJobs.RunsAsync(api, NoticeJobs.Retention, ct);

        Quartz.JobExecutionException? escaped;
        await SetConnectionsAsync(database, allow: false, ct);
        try
        {
            using (var pooled = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooled);
            }

            escaped = await runs.RunAndReportAsync(ct);
        }
        finally
        {
            await SetConnectionsAsync(database, allow: true, CancellationToken.None);
        }

        var failures = api.Logs.Where(log => log.EventId.Name == "NoticesRetentionRunFailed").ToList();
        failures.ShouldNotBeEmpty("o job de retenção com o banco fora não registrou NoticesRetentionRunFailed");
        failures.ShouldAllBe(log => log.LogLevel == LogLevel.Error, "NoticesRetentionRunFailed não é Error");
        escaped.ShouldBeNull($"a falha da retenção escapou para o Quartz em vez de virar log: {escaped}");
    }

    /// <summary>Liga ou desliga as conexões ao database <paramref name="database"/>; ao desligar, derruba as abertas.</summary>
    private async Task SetConnectionsAsync(string database, bool allow, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // nome gerado pelo teste (guid), sem entrada externa
        await using (var alter = new NpgsqlCommand($"alter database \"{database}\" with allow_connections {(allow ? "true" : "false")}", connection))
#pragma warning restore CA2100
        {
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!allow)
        {
            await using var terminate = new NpgsqlCommand(
                "select pg_terminate_backend(pid) from pg_stat_activity where datname = @database and pid <> pg_backend_pid()",
                connection);
            terminate.Parameters.AddWithValue("database", database);
            await terminate.ExecuteNonQueryAsync(ct);
        }
    }
}
