using Npgsql;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0032 H31-07 (item herdado da revisão da SPEC-0031; contrato v2) — depois das migrações do Notifications (aplicadas
/// na subida da API), <c>notifications.deliveries</c> tem o índice <c>ix_deliveries_created_at_utc</c> sobre
/// <c>created_at_utc</c> (o <c>delete</c> da retenção não varre a tabela).
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class DeliveriesRetentionIndexTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0032:H31-07")]
    [Trait("Kind", "Integration")]
    public async Task TheDeliveriesHaveTheCreatedAtIndexAfterTheMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select a.attname
            from pg_index i
            join pg_class ix on ix.oid = i.indexrelid
            join pg_class t on t.oid = i.indrelid
            join pg_namespace n on n.oid = t.relnamespace
            join pg_attribute a on a.attrelid = t.oid and a.attnum = any(i.indkey)
            where n.nspname = 'notifications' and t.relname = 'deliveries' and ix.relname = 'ix_deliveries_created_at_utc'
            """,
            connection);
        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                columns.Add(reader.GetString(0));
            }
        }

        columns.ShouldBe(["created_at_utc"], "notifications.deliveries sem o índice ix_deliveries_created_at_utc(created_at_utc)");
    }
}
