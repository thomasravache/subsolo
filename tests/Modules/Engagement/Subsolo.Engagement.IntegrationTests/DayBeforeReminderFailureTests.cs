using Microsoft.Extensions.Logging;
using Npgsql;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 IT-04 (Emenda 3; Plano de Rollout §8: falha do job vira alerta no Seq) — com o host no ar e o banco dele
/// fora do ar (o database passa a recusar conexões e as abertas são derrubadas), o job <c>Engagement.DayBeforeReminder</c>
/// registra o evento <c>DayBeforeReminderRunFailed</c> com nível Error e não deixa a exceção escapar para o Quartz (a
/// próxima hora tenta de novo). O filtro do alerta "Falha de job" é conferido no <c>DayBeforeReminderRunFailedAlertTests</c>
/// do Observability.UnitTests, com a mesma tag. O banco volta antes do fim do teste (como o SPEC-0031:IT-07).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DayBeforeReminderFailureTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0032:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WithTheDatabaseDownTheReminderJobLogsDayBeforeReminderRunFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var api = new EngagementApiFactory(connectionString, settings: ReminderRequests.ManualReminderSettings());
        api.CreateEngagementClient().Dispose();
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DayBeforeReminder, ct);

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

        var failures = api.Logs.Where(log => log.EventId.Name == "DayBeforeReminderRunFailed").ToList();
        failures.ShouldNotBeEmpty("o job do lembrete com o banco fora não registrou DayBeforeReminderRunFailed");
        failures.ShouldAllBe(log => log.LogLevel == LogLevel.Error, "DayBeforeReminderRunFailed não é Error");
        escaped.ShouldBeNull($"a falha do lembrete escapou para o Quartz em vez de virar log: {escaped}");
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
