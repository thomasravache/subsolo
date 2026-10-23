using Npgsql;
using NpgsqlTypes;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0030 — leitura e carga direta das tabelas do §6, no schema <c>notifications</c>:
/// <c>push_subscriptions(id, user_id, endpoint UNIQUE, p256dh, auth, created_at_utc, last_success_at_utc)</c>,
/// <c>preferences(user_id PK, new_show_push, daily_digest_email, reminder_push, reminder_email)</c> e
/// <c>deliveries(user_id, dedupe_key, kind, channel, status, attempts, created_at_utc)</c>. Os usuários não precisam
/// existir no Users (as tabelas guardam só o id, ADR-0001).
/// </summary>
internal static class NotificationsDatabase
{
    public static async Task<IReadOnlyList<SubscriptionRow>> SubscriptionsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<SubscriptionRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select user_id, endpoint, p256dh, auth from notifications.push_subscriptions", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new SubscriptionRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<DeliveryRow>> DeliveriesAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<DeliveryRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select user_id, dedupe_key, kind, channel, status, attempts from notifications.deliveries", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new DeliveryRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                Convert.ToString(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    /// <summary>Quantas linhas de <paramref name="userId"/> há em cada tabela do schema (inscrições, preferências, entregas).</summary>
    public static async Task<(long Subscriptions, long Preferences, long Deliveries)> RowsOfAsync(
        string connectionString, Guid userId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            select (select count(*) from notifications.push_subscriptions where user_id = @user),
                   (select count(*) from notifications.preferences where user_id = @user),
                   (select count(*) from notifications.deliveries where user_id = @user)
            """,
            connection);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    /// <summary>Grava direto (COPY binário) as preferências de cada usuário.</summary>
    public static async Task SeedPreferencesAsync(
        string connectionString, IEnumerable<(Guid UserId, bool NewShowPush, bool DailyDigestEmail, bool ReminderPush, bool ReminderEmail)> rows, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy notifications.preferences (user_id, new_show_push, daily_digest_email, reminder_push, reminder_email) from stdin (format binary)", ct);
        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(row.UserId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.NewShowPush, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(row.DailyDigestEmail, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(row.ReminderPush, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(row.ReminderEmail, NpgsqlDbType.Boolean, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>Grava direto (COPY binário) uma inscrição de push por linha (<c>last_success_at_utc</c> nulo).</summary>
    public static async Task SeedSubscriptionsAsync(
        string connectionString, IEnumerable<(Guid UserId, string Endpoint, string P256dh, string Auth)> rows, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy notifications.push_subscriptions (id, user_id, endpoint, p256dh, auth, created_at_utc) from stdin (format binary)", ct);
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.UserId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.Endpoint, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.P256dh, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.Auth, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
        }

        await writer.CompleteAsync(ct);
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

internal sealed record SubscriptionRow(Guid UserId, string Endpoint, string P256dh, string Auth);

internal sealed record DeliveryRow(Guid UserId, string DedupeKey, string Kind, string Channel, string Status, int Attempts);
