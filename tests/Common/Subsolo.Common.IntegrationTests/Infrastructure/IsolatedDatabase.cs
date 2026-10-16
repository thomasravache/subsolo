using Npgsql;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Common.IntegrationTests.Infrastructure;

/// <summary>
/// Um database novo no container do <see cref="PostgresFixture"/> para cada teste: migrações e schemas de um teste
/// não interferem nos outros.
/// </summary>
internal static class IsolatedDatabase
{
    public static async Task<string> CreateAsync(PostgresFixture postgres, CancellationToken cancellationToken)
    {
        var name = $"it_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            // Nome gerado aqui (guid), sem entrada externa.
#pragma warning disable CA2100 // SQL montado só com valores gerados ou constantes do próprio teste
            await using var create = new NpgsqlCommand($"create database \"{name}\"", connection);
#pragma warning restore CA2100
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
    }
}
