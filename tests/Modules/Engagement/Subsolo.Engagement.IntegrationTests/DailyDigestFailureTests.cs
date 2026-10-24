using Npgsql;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0031 IT-07 (Emenda 3; Plano de Rollout §8: falha do job vira alerta no Seq) — com o host no ar e o banco dele
/// fora do ar (o database passa a recusar conexões e as abertas são derrubadas), o job <c>Engagement.DailyDigest</c>
/// registra o evento <c>DailyDigestRunFailed</c> (a base do alerta "Falha de job"; o filtro do alerta é conferido no
/// <c>JobFailureAlertCoversNoticeDeliveriesTests</c> do Observability.UnitTests, com a mesma tag). O banco volta antes do
/// fim do teste.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DailyDigestFailureTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0031:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task WithTheDatabaseDownTheDigestJobLogsDailyDigestRunFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var api = new EngagementApiFactory(connectionString);
        api.CreateEngagementClient().Dispose();
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DailyDigest, ct);

        await SetConnectionsAsync(database, allow: false, ct);
        try
        {
            using (var pooled = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooled);
            }

            await runs.RunAndReportAsync(ct);
        }
        finally
        {
            await SetConnectionsAsync(database, allow: true, CancellationToken.None);
        }

        api.Logs.Select(log => log.EventId.Name)
            .ShouldContain("DailyDigestRunFailed", "o job do resumo com o banco fora não registrou DailyDigestRunFailed");
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
