using Npgsql;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>SPEC-0023 — leitura direta de <c>events.shows</c> e da outbox do Events (§6).</summary>
internal static class ShowsDatabase
{
    public static async Task<IReadOnlyList<ShowRow>> ShowsAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            select id, slug, city, title, starts_at_utc, organizer_profile_id, venue_profile_id, venue_name, lineup::text,
                   styles, price, ticket_url, flyer_id, status, created_by
            from events.shows order by starts_at_utc, id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<ShowRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ShowRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetGuid(5),
                await reader.IsDBNullAsync(6, ct) ? null : reader.GetGuid(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetFieldValue<string[]>(9),
                reader.GetString(10),
                await reader.IsDBNullAsync(11, ct) ? null : reader.GetString(11),
                reader.GetString(12).Trim(),
                reader.GetString(13),
                reader.GetGuid(14)));
        }

        return rows;
    }

    public static async Task<long> CountAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("select count(*) from events.shows", connection);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>O conteúdo (JSON) das mensagens da outbox do Events cujo tipo contém <paramref name="typeFragment"/>.</summary>
    public static async Task<IReadOnlyList<string>> OutboxContentsAsync(string connectionString, string typeFragment, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select content::text from events.outbox_messages where type like '%' || @fragment || '%' order by occurred_on_utc",
            connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var contents = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            contents.Add(reader.GetString(0));
        }

        return contents;
    }

    internal sealed record ShowRow(
        Guid Id,
        string Slug,
        string City,
        string Title,
        DateTimeOffset StartsAtUtc,
        Guid OrganizerProfileId,
        Guid? VenueProfileId,
        string VenueName,
        string Lineup,
        string[] Styles,
        string Price,
        string? TicketUrl,
        string FlyerId,
        string Status,
        Guid CreatedBy);
}
