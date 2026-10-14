using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — PostgreSQL 18 real (imagem <c>postgres:18.6</c>) num container efêmero, compartilhado por coleção
/// de testes (<see cref="PostgresCollection"/>). Entre testes, <see cref="ResetAsync"/> limpa as tabelas dos
/// schemas indicados com <c>TRUNCATE</c>.
/// </summary>
/// <remarks>
/// Sem Docker disponível, a inicialização falha logo com a mensagem
/// "Docker não encontrado: inicie o Docker para rodar testes de integração".
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Imagem com tag fixa (ADR-0002, ADR-0009).</summary>
    public const string Image = "postgres:18.6";

    /// <summary>Tabela de histórico de migrations do EF Core: nunca é esvaziada pelo <see cref="ResetAsync"/>.</summary>
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    private PostgreSqlContainer? _container;

    /// <summary>String de conexão do banco do container (válida depois de <see cref="InitializeAsync"/>).</summary>
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgresFixture ainda não inicializado (InitializeAsync).");

    /// <summary>
    /// Esvazia (<c>TRUNCATE … RESTART IDENTITY CASCADE</c>) todas as tabelas dos schemas informados; sem schemas,
    /// as de todos os schemas do usuário. O histórico de migrations do EF Core é preservado.
    /// </summary>
    public async Task ResetAsync(params string[] schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var select = new NpgsqlCommand(
            """
            select format('%I.%I', schemaname, tablename)
            from pg_catalog.pg_tables
            where tablename <> @history
              and (cardinality(@schemas) = 0 and schemaname not in ('pg_catalog', 'information_schema')
                   or schemaname = any(@schemas))
            order by 1
            """,
            connection))
        {
            select.Parameters.AddWithValue("history", MigrationsHistoryTable);
            select.Parameters.AddWithValue("schemas", schemas);
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        // Nomes vêm do catálogo, já citados por format('%I.%I').
#pragma warning disable CA2100
        await using var truncate = new NpgsqlCommand(
            $"truncate table {string.Join(", ", tables)} restart identity cascade", connection);
#pragma warning restore CA2100
        await truncate.ExecuteNonQueryAsync();
    }

    public async ValueTask InitializeAsync() =>
        _container = await DockerEnvironment.StartAsync(() => new PostgreSqlBuilder(Image).Build());

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
