using Npgsql;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Infrastructure;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-02 — as migrações do <c>ModuleDbContext</c> do módulo de exemplo criam todas as tabelas
/// (inclusive outbox, inbox e o histórico de migrações) só no schema <c>template</c> (ADR-0002).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SchemaPerModuleTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0007:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task MigrationsCreateEveryTableOnlyInTheModuleSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);

        await host.MigrateAsync<TemplateDbContext>(ct);

        var tables = await UserTablesAsync(host.ConnectionString, ct);
        tables.ShouldNotBeEmpty("as migrações não criaram nenhuma tabela");
        tables.Where(t => t.Schema != TemplateDbContext.SchemaName)
            .ShouldBeEmpty($"tabelas fora do schema {TemplateDbContext.SchemaName}: {string.Join(", ", tables)}");
        var names = tables.Select(t => t.Name).ToList();
        foreach (var expected in new[] { "outbox_messages", "inbox_messages", "items" })
        {
            names.ShouldContain(expected, $"tabelas encontradas: {string.Join(", ", tables)}");
        }
    }

    private static async Task<List<(string Schema, string Name)>> UserTablesAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select table_schema, table_name
            from information_schema.tables
            where table_schema not in ('pg_catalog', 'information_schema')
            order by 1, 2
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var tables = new List<(string, string)>();
        while (await reader.ReadAsync(ct))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }
}
