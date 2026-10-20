using Npgsql;
using NpgsqlTypes;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// Leitura e carga direta da tabela do §6: <c>engagement.follows(user_id, target_kind, target_key, created_at_utc)</c>.
/// </summary>
internal static class EngagementDatabase
{
    public static async Task<IReadOnlyList<FollowRow>> FollowsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<FollowRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select user_id, target_kind, target_key from engagement.follows order by created_at_utc", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new FollowRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>Os follows de <paramref name="userId"/>.</summary>
    public static async Task<IReadOnlyList<FollowRow>> FollowsOfAsync(string connectionString, Guid userId, CancellationToken ct) =>
        [.. (await FollowsAsync(connectionString, ct)).Where(row => row.UserId == userId)];

    /// <summary>
    /// Grava direto (COPY binário) um follow de cada usuário de <paramref name="userIds"/> para o alvo; os usuários não
    /// precisam existir no Users (a tabela guarda só o id, ADR-0001).
    /// </summary>
    public static async Task SeedAsync(
        string connectionString, IReadOnlyCollection<Guid> userIds, string targetKind, string targetKey, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy engagement.follows (user_id, target_kind, target_key, created_at_utc) from stdin (format binary)", ct);
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(targetKind, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(targetKey, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>Atualiza as estatísticas da tabela (o plano da consulta medida usa o índice por alvo).</summary>
    public static async Task AnalyzeAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("analyze engagement.follows", connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}

internal sealed record FollowRow(Guid UserId, string TargetKind, string TargetKey);

/// <summary>
/// SPEC-0029 — leitura e carga direta da tabela do §6: <c>engagement.attendances(user_id, show_id, created_at_utc)</c>.
/// </summary>
internal static class AttendanceDatabase
{
    public static async Task<IReadOnlyList<AttendanceRow>> AttendancesAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<AttendanceRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select user_id, show_id from engagement.attendances order by created_at_utc, user_id", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new AttendanceRow(reader.GetGuid(0), reader.GetGuid(1)));
        }

        return rows;
    }

    /// <summary>Os "vou" de <paramref name="userId"/>.</summary>
    public static async Task<IReadOnlyList<AttendanceRow>> AttendancesOfAsync(string connectionString, Guid userId, CancellationToken ct) =>
        [.. (await AttendancesAsync(connectionString, ct)).Where(row => row.UserId == userId)];

    /// <summary>
    /// Grava direto (COPY binário) um "vou" de cada usuário de <paramref name="userIds"/> no show; nem os usuários nem o
    /// show precisam existir nos outros módulos (a tabela guarda só ids, ADR-0001).
    /// </summary>
    public static async Task SeedAsync(string connectionString, Guid showId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy engagement.attendances (user_id, show_id, created_at_utc) from stdin (format binary)", ct);
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(showId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>Atualiza as estatísticas da tabela (o plano da consulta medida usa o índice por show).</summary>
    public static async Task AnalyzeAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("analyze engagement.attendances", connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}

internal sealed record AttendanceRow(Guid UserId, Guid ShowId);
