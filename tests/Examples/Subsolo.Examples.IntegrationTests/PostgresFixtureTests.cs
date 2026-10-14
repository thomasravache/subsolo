using Npgsql;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 IT-01 — o <see cref="PostgresFixture"/> sobe um PostgreSQL 18 real (Testcontainers) e a string de
/// conexão exposta aponta para ele.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PostgresFixtureTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0005:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task SelectVersionReportsPostgreSql18()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("select version()", connection);

        var version = (string?)await command.ExecuteScalarAsync(ct);

        version.ShouldNotBeNull();
        version.ShouldStartWith("PostgreSQL 18");
    }
}
