using Npgsql;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>SPEC-0022 — leitura direta de <c>events.flyers</c> (id, uploaded_by, created_at_utc, attached_at_utc), §6.</summary>
internal static class FlyersDatabase
{
    public static async Task<IReadOnlyList<FlyerRow>> FlyersAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select id, uploaded_by, created_at_utc, attached_at_utc from events.flyers order by created_at_utc, id",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<FlyerRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new FlyerRow(
                reader.GetString(0).Trim(),
                reader.GetGuid(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                await reader.IsDBNullAsync(3, ct) ? null : reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return rows;
    }

    internal sealed record FlyerRow(string Id, Guid UploadedBy, DateTimeOffset CreatedAtUtc, DateTimeOffset? AttachedAtUtc);
}
