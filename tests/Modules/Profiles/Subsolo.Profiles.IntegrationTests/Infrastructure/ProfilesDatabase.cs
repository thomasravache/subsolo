using Npgsql;

namespace Subsolo.Profiles.IntegrationTests.Infrastructure;

/// <summary>
/// Leitura direta das tabelas do §6 (schema <c>profiles</c>: <c>profiles</c>, <c>profile_requests</c>,
/// <c>outbox_messages</c>) e, para o papel <c>artist</c>, de <c>users.users</c> e <c>users.inbox_messages</c>.
/// </summary>
internal static class ProfilesDatabase
{
    public static async Task<IReadOnlyList<ProfileRow>> ProfilesAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<ProfileRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select id, type, name, slug, city, owner_user_id from profiles.profiles order by created_at_utc", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ProfileRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<RequestRow>> RequestsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<RequestRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select id, kind, requester_user_id, profile_id, status, reason, contact_or_proof, decided_at_utc
            from profiles.profile_requests
            order by created_at_utc
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new RequestRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc))));
        }

        return rows;
    }

    /// <summary>O <c>content</c> (JSON) das mensagens da outbox do Profiles cujo tipo contém <paramref name="typeFragment"/>.</summary>
    public static Task<IReadOnlyList<string>> OutboxContentsAsync(string connectionString, string typeFragment, CancellationToken ct) =>
        TextsAsync(
            connectionString,
            "select content::text from profiles.outbox_messages where type like '%' || @fragment || '%' order by occurred_on_utc",
            typeFragment,
            ct);

    /// <summary>Os papéis de <paramref name="userId"/> em <c>users.users</c> (texto do array), ou nulo se o usuário não existe.</summary>
    public static async Task<string?> UserRolesAsync(string connectionString, Guid userId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("select roles::text from users.users where id = @id", connection);
        command.Parameters.AddWithValue("id", userId);
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    /// <summary>As mensagens da inbox do Users cujo tipo contém <paramref name="typeFragment"/>: (id, processada?).</summary>
    public static async Task<IReadOnlyList<(Guid Id, bool Processed)>> UsersInboxAsync(
        string connectionString, string typeFragment, CancellationToken ct)
    {
        var rows = new List<(Guid, bool)>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select id, processed_on_utc is not null from users.inbox_messages where type like '%' || @fragment || '%'", connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add((reader.GetGuid(0), reader.GetBoolean(1)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> TextsAsync(string connectionString, string sql, string fragment, CancellationToken ct)
    {
        var texts = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // SQL fixo dos chamadores deste arquivo; o valor vai por parâmetro
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        command.Parameters.AddWithValue("fragment", fragment);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            texts.Add(reader.GetString(0));
        }

        return texts;
    }
}

internal sealed record ProfileRow(Guid Id, string Type, string Name, string Slug, string City, Guid? OwnerUserId);

internal sealed record RequestRow(
    Guid Id,
    string Kind,
    Guid RequesterUserId,
    Guid? ProfileId,
    string Status,
    string? Reason,
    string? ContactOrProof,
    DateTimeOffset? DecidedAtUtc);
