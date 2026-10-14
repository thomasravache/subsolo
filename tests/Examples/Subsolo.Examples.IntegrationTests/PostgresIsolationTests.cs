using Npgsql;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 IT-03 — dois testes da mesma coleção gravam na mesma tabela depois de
/// <see cref="PostgresFixture.ResetAsync"/>; em qualquer ordem, cada um enxerga exatamente a própria linha
/// (container compartilhado, dados isolados por <c>TRUNCATE</c> do schema).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PostgresIsolationTests(PostgresFixture db)
{
    private const string Schema = "harness_examples";

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-03")]
    [Trait("Kind", "Integration")]
    public Task FirstWriterSeesOnlyItsOwnRowAfterReset() => InsertAfterResetAndExpectOnlyOwnRowAsync("primeiro");

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-03")]
    [Trait("Kind", "Integration")]
    public Task SecondWriterSeesOnlyItsOwnRowAfterReset() => InsertAfterResetAndExpectOnlyOwnRowAsync("segundo");

    private async Task InsertAfterResetAndExpectOnlyOwnRowAsync(string owner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(
            connection,
            $"create schema if not exists {Schema}; " +
            $"create table if not exists {Schema}.shows (id bigint generated always as identity primary key, owner text not null)",
            ct);

        await db.ResetAsync(Schema);

        await using (var insert = new NpgsqlCommand($"insert into {Schema}.shows (owner) values (@owner)", connection))
        {
            insert.Parameters.AddWithValue("owner", owner);
            await insert.ExecuteNonQueryAsync(ct);
        }

        var owners = new List<string>();
        await using (var select = new NpgsqlCommand($"select owner from {Schema}.shows", connection))
        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                owners.Add(reader.GetString(0));
            }
        }

        owners.ShouldBe([owner]);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
