using Npgsql;
using NpgsqlTypes;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0031 — leitura e carga direta das tabelas que os avisos de show novo e o resumo diário tocam:
/// <list type="bullet">
/// <item><c>engagement.published_shows(show_id PK, city_slug, profile_ids uuid[], published_at_utc)</c> (§6);</item>
/// <item><c>engagement.inbox_messages</c> (a inbox do módulo, SPEC-0007), para entregar o mesmo evento de novo;</item>
/// <item>as do Notifications (SPEC-0030): <c>preferences</c>, <c>push_subscriptions</c>, <c>notices</c> e
/// <c>deliveries</c>;</item>
/// <item><c>users.users</c> (o e-mail que o <c>IRecipientDirectory</c> devolve ao Notifications).</item>
/// </list>
/// Os usuários semeados não precisam ter entrado (as tabelas guardam só o id, ADR-0001).
/// </summary>
internal static class NoticesDatabase
{
    /// <summary>As entregas do Notifications (<c>user_id, dedupe_key, kind, channel</c>).</summary>
    public static async Task<IReadOnlyList<NoticeDeliveryRow>> DeliveriesAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<NoticeDeliveryRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select user_id, dedupe_key, kind, channel from notifications.deliveries", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new NoticeDeliveryRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    /// <summary>Quantas entregas do tipo <paramref name="kind"/> e canal <paramref name="channel"/> existem.</summary>
    public static async Task<long> CountDeliveriesAsync(string connectionString, string kind, string channel, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select count(*) from notifications.deliveries where kind = @kind and channel = @channel", connection);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("channel", channel);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Os avisos gravados (<c>dedupe_key, kind, title, body, url</c>).</summary>
    public static async Task<IReadOnlyList<NoticeRow>> NoticesAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<NoticeRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select dedupe_key, kind, title, body, url from notifications.notices", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new NoticeRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }

        return rows;
    }

    /// <summary>As linhas de <c>engagement.published_shows</c>.</summary>
    public static async Task<IReadOnlyList<PublishedShowRow>> PublishedShowsAsync(string connectionString, CancellationToken ct)
    {
        var rows = new List<PublishedShowRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            "select show_id, city_slug, profile_ids, published_at_utc from engagement.published_shows", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new PublishedShowRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetFieldValue<Guid[]>(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return rows;
    }

    /// <summary>Grava linhas em <c>engagement.published_shows</c> (a mesma linha de novo não muda nada).</summary>
    public static async Task SeedPublishedShowsAsync(
        string connectionString, IEnumerable<PublishedShowRow> rows, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        foreach (var row in rows)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into engagement.published_shows (show_id, city_slug, profile_ids, published_at_utc)
                values (@show, @city, @profiles, @at)
                on conflict (show_id) do nothing
                """,
                connection);
            command.Parameters.AddWithValue("show", row.ShowId);
            command.Parameters.AddWithValue("city", row.CitySlug);
            command.Parameters.Add(new NpgsqlParameter<Guid[]>("profiles", [.. row.ProfileIds]));
            command.Parameters.AddWithValue("at", row.PublishedAtUtc);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>As mensagens da inbox do Engagement cujo tipo cita <paramref name="typeFragment"/>.</summary>
    public static async Task<IReadOnlyList<InboxRow>> InboxAsync(string connectionString, string typeFragment, CancellationToken ct)
    {
        var rows = new List<InboxRow>();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            select id, processed_on_utc, error from engagement.inbox_messages
            where type like '%' || @fragment || '%'
            order by occurred_on_utc
            """,
            connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InboxRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTime>(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>
    /// Entrega de novo à inbox do Engagement a mensagem <paramref name="messageId"/> (mesmo tipo e conteúdo, id novo e
    /// ainda não processada), como um evento repetido pelo barramento; devolve o id novo.
    /// </summary>
    public static async Task<Guid> RedeliverAsync(string connectionString, Guid messageId, CancellationToken ct)
    {
        var copy = Guid.CreateVersion7();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            insert into engagement.inbox_messages (id, type, content, occurred_on_utc, processed_on_utc, attempts, next_attempt_on_utc, error)
            select @copy, type, content, occurred_on_utc, null, 0, null, null
            from engagement.inbox_messages where id = @id
            """,
            connection);
        command.Parameters.AddWithValue("copy", copy);
        command.Parameters.AddWithValue("id", messageId);
        (await command.ExecuteNonQueryAsync(ct)).ShouldBe(1, $"mensagem {messageId} não está na inbox do Engagement");
        return copy;
    }

    /// <summary>Grava (COPY binário) um usuário por id em <c>users.users</c>, com o e-mail de <paramref name="email"/>.</summary>
    public static async Task SeedUsersAsync(
        string connectionString, IReadOnlyCollection<Guid> userIds, Func<Guid, string> email, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy users.users (id, email, display_name, roles, created_at_utc) from stdin (format binary)", ct);
        var now = DateTime.UtcNow;
        foreach (var id in userIds)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(id, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(email(id), NpgsqlDbType.Varchar, ct);
            await writer.WriteNullAsync(ct);
            await writer.WriteAsync(Array.Empty<string>(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>Grava (COPY binário) as preferências de aviso de cada usuário.</summary>
    public static async Task SeedPreferencesAsync(
        string connectionString,
        IEnumerable<(Guid UserId, bool NewShowPush, bool DailyDigestEmail)> rows,
        CancellationToken ct)
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
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>Grava (COPY binário) um aparelho inscrito (endpoint do FCM, chaves de teste) por usuário.</summary>
    public static async Task SeedSubscriptionsAsync(string connectionString, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy notifications.push_subscriptions (id, user_id, endpoint, p256dh, auth, created_at_utc) from stdin (format binary)", ct);
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync($"https://fcm.googleapis.com/fcm/send/spec0031-{userId:N}", NpgsqlDbType.Text, ct);
            await writer.WriteAsync("p256dh-spec0031", NpgsqlDbType.Text, ct);
            await writer.WriteAsync("auth-spec0031", NpgsqlDbType.Text, ct);
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

internal sealed record NoticeDeliveryRow(Guid UserId, string DedupeKey, string Kind, string Channel);

internal sealed record NoticeRow(string DedupeKey, string Kind, string Title, string Body, string Url);

internal sealed record PublishedShowRow(Guid ShowId, string CitySlug, IReadOnlyList<Guid> ProfileIds, DateTimeOffset PublishedAtUtc);

internal sealed record InboxRow(Guid Id, DateTime? ProcessedOnUtc, string? Error);
