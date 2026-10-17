using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Subsolo.Common.Infrastructure.Observability;

/// <summary>
/// SPEC-0009 — entrada <see cref="Name"/> do <c>/health</c>: Healthy quando o banco da aplicação responde a um
/// <c>select 1</c> dentro do <see cref="Timeout"/> do registro; Unhealthy caso contrário.
/// </summary>
internal sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    /// <summary>Nome da entrada no <c>/health</c> (contrato: <c>entries.postgres</c>).</summary>
    public const string Name = "postgres";

    /// <summary>Tempo máximo da verificação (banco travado conta como fora do ar).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(CommonInfrastructureExtensions.ConnectionString(configuration));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("select 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL indisponível", exception);
        }
    }
}
