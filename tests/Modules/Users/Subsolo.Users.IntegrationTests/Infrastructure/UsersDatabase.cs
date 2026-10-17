using Npgsql;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>Leitura direta das tabelas do schema <c>users</c> (§6: <c>users</c>, <c>sessions</c>, <c>outbox_messages</c>).</summary>
internal static class UsersDatabase
{
    public static async Task<IReadOnlyList<UserRow>> UsersAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<UserRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select id, email, display_name, roles::text from users.users order by created_at_utc", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new UserRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<SessionRow>> SessionsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<SessionRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select user_id, method, created_at_utc, last_seen_at_utc, expires_at_utc, absolute_expires_at_utc,
                   revoked_at_utc, row_to_json(s)::text, id_hash::text
            from users.sessions s
            order by created_at_utc
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new SessionRow(
                reader.GetGuid(0),
                reader.GetString(1),
                Utc(reader.GetDateTime(2)),
                Utc(reader.GetDateTime(3)),
                Utc(reader.GetDateTime(4)),
                Utc(reader.GetDateTime(5)),
                reader.IsDBNull(6) ? null : Utc(reader.GetDateTime(6)),
                reader.GetString(7),
                reader.GetString(8)));
        }

        return rows;
    }

    /// <summary>Quantas mensagens da outbox do módulo Users têm um tipo que contém <paramref name="typeFragment"/>.</summary>
    public static async Task<long> OutboxCountAsync(string connectionString, string typeFragment, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select count(*) from users.outbox_messages where type like '%' || @fragment || '%'", connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

internal sealed record UserRow(Guid Id, string Email, string? DisplayName, string Roles);

/// <summary>
/// Uma linha de <c>users.sessions</c>; <see cref="Json"/> é a linha inteira (todas as colunas) em JSON e
/// <see cref="IdHash"/> é o <c>id_hash</c> como texto (<c>bytea</c> vira <c>\x…</c> em hex).
/// </summary>
internal sealed record SessionRow(
    Guid UserId,
    string Method,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string Json,
    string IdHash);
