using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Modules.Notifications.Application;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;
using Subsolo.Modules.Notifications.Domain.Notices;
using Subsolo.Modules.Notifications.Domain.Preferences;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// SPEC-0030 — <see cref="INotificationsStore"/> com SQL direto na conexão do <see cref="NotificationsDbContext"/>: as
/// leituras e gravações de muitos usuários vão num comando só (arrays com <c>unnest</c>), e as que precisam ser atômicas
/// (a primeira inscrição liga o show novo) numa transação.
/// </summary>
internal sealed class NotificationsStore(NotificationsDbContext db) : INotificationsStore
{
    public async Task SubscribeAsync(
        Guid userId, string endpoint, string p256dh, string auth, DateTimeOffset now, CancellationToken ct)
    {
        var connection = await OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var subscribe = Command(
            connection,
            """
            insert into notifications.push_subscriptions (id, user_id, endpoint, p256dh, auth, created_at_utc)
            values (@id, @user, @endpoint, @p256dh, @auth, @now)
            on conflict (endpoint) do update
                set user_id = excluded.user_id, p256dh = excluded.p256dh, auth = excluded.auth
            """))
        {
            subscribe.Transaction = transaction;
            subscribe.Parameters.AddWithValue("id", Guid.CreateVersion7(now));
            subscribe.Parameters.AddWithValue("user", userId);
            subscribe.Parameters.AddWithValue("endpoint", endpoint);
            subscribe.Parameters.AddWithValue("p256dh", p256dh);
            subscribe.Parameters.AddWithValue("auth", auth);
            subscribe.Parameters.AddWithValue("now", now);
            await subscribe.ExecuteNonQueryAsync(ct);
        }

        // Ativar o push pela primeira vez liga o show novo (§2, Emenda de 2026-10-23) e grava as preferências. SPEC-0031
        // (H30-07): só para quem ainda não tem a linha de preferências; quem já escolheu (por exemplo, desligou o show
        // novo) e reinscreve o aparelho que o servidor perdeu (410) mantém as escolhas.
        await using (var turnOn = Command(
            connection,
            """
            insert into notifications.preferences (user_id, new_show_push, daily_digest_email, reminder_push, reminder_email)
            values (@user, true, @digest, @reminderPush, @reminderEmail)
            on conflict (user_id) do nothing
            """))
        {
            turnOn.Transaction = transaction;
            turnOn.Parameters.AddWithValue("user", userId);
            turnOn.Parameters.AddWithValue("digest", NotificationPreferences.Default.DailyDigestEmail);
            turnOn.Parameters.AddWithValue("reminderPush", NotificationPreferences.Default.ReminderPush);
            turnOn.Parameters.AddWithValue("reminderEmail", NotificationPreferences.Default.ReminderEmail);
            await turnOn.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct)
    {
        await using var command = Command(
            await OpenAsync(ct),
            "delete from notifications.push_subscriptions where user_id = @user and endpoint = @endpoint");
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("endpoint", endpoint);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<(NotificationPreferences Preferences, int PushDevices)> PreferencesAsync(Guid userId, CancellationToken ct)
    {
        await using var command = Command(
            await OpenAsync(ct),
            """
            select p.new_show_push, p.daily_digest_email, p.reminder_push, p.reminder_email,
                   (select count(*) from notifications.push_subscriptions s where s.user_id = @user)
            from (select @user as user_id) u
            left join notifications.preferences p on p.user_id = u.user_id
            """);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var preferences = reader.IsDBNull(0)
            ? NotificationPreferences.Default
            : new NotificationPreferences(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3));
        return (preferences, checked((int)reader.GetInt64(4)));
    }

    public async Task SavePreferencesAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await using var command = Command(
            await OpenAsync(ct),
            """
            insert into notifications.preferences (user_id, new_show_push, daily_digest_email, reminder_push, reminder_email)
            values (@user, @newShow, @digest, @reminderPush, @reminderEmail)
            on conflict (user_id) do update set
                new_show_push = excluded.new_show_push,
                daily_digest_email = excluded.daily_digest_email,
                reminder_push = excluded.reminder_push,
                reminder_email = excluded.reminder_email
            """);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("newShow", preferences.NewShowPush);
        command.Parameters.AddWithValue("digest", preferences.DailyDigestEmail);
        command.Parameters.AddWithValue("reminderPush", preferences.ReminderPush);
        command.Parameters.AddWithValue("reminderEmail", preferences.ReminderEmail);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task TurnOffEmailAsync(Guid userId, string kind, CancellationToken ct)
    {
        // Sem linha de preferências, o e-mail já está desligado (padrão): nada a gravar.
        var column = kind == NoticeKinds.Digest ? "daily_digest_email" : "reminder_email";
#pragma warning disable CA2100 // a coluna vem da lista fechada acima, nunca da entrada
        await using var command = Command(
            await OpenAsync(ct), $"update notifications.preferences set {column} = false where user_id = @user");
#pragma warning restore CA2100
        command.Parameters.AddWithValue("user", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<RecipientState>> RecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        await using var command = Command(
            await OpenAsync(ct),
            """
            select u.id, p.new_show_push, p.daily_digest_email, p.reminder_push, p.reminder_email,
                   exists (select 1 from notifications.push_subscriptions s where s.user_id = u.id)
            from unnest(@ids) as u(id)
            left join notifications.preferences p on p.user_id = u.id
            """);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("ids", [.. userIds]));
        var states = new List<RecipientState>(userIds.Count);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var preferences = reader.IsDBNull(1)
                ? NotificationPreferences.Default
                : new NotificationPreferences(reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4));
            states.Add(new RecipientState(reader.GetGuid(0), preferences, reader.GetBoolean(5)));
        }

        return states;
    }

    public async Task<IReadOnlySet<PlannedDelivery>> RegisteredAsync(
        string dedupeKey, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        await using var command = Command(
            await OpenAsync(ct),
            "select user_id, channel from notifications.deliveries where dedupe_key = @key and user_id = any(@ids)");
        command.Parameters.AddWithValue("key", dedupeKey);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("ids", [.. userIds]));
        var registered = new HashSet<PlannedDelivery>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            registered.Add(new PlannedDelivery(reader.GetGuid(0), ChannelOf(reader.GetString(1))));
        }

        return registered;
    }

    public async Task AddDeliveriesAsync(
        Notice notice, IReadOnlyList<PlannedDelivery> deliveries, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(deliveries);
        var connection = await OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var noticeId = Guid.CreateVersion7(now);
        await using (var insertNotice = Command(
            connection,
            """
            insert into notifications.notices (id, dedupe_key, kind, title, body, url, created_at_utc)
            values (@id, @key, @kind, @title, @body, @url, @now)
            """))
        {
            insertNotice.Transaction = transaction;
            insertNotice.Parameters.AddWithValue("id", noticeId);
            insertNotice.Parameters.AddWithValue("key", notice.DedupeKey);
            insertNotice.Parameters.AddWithValue("kind", notice.Kind);
            insertNotice.Parameters.AddWithValue("title", notice.Title);
            insertNotice.Parameters.AddWithValue("body", notice.Body);
            insertNotice.Parameters.AddWithValue("url", notice.Url);
            insertNotice.Parameters.AddWithValue("now", now);
            await insertNotice.ExecuteNonQueryAsync(ct);
        }

        await using (var insertDeliveries = Command(
            connection,
            """
            insert into notifications.deliveries
                (notice_id, user_id, dedupe_key, kind, channel, status, attempts, created_at_utc, next_attempt_at_utc)
            select @notice, d.user_id, @key, @kind, d.channel, @pending, 0, @now, @now
            from unnest(@users, @channels) as d(user_id, channel)
            on conflict (user_id, dedupe_key, channel) do nothing
            """))
        {
            insertDeliveries.Transaction = transaction;
            insertDeliveries.Parameters.AddWithValue("notice", noticeId);
            insertDeliveries.Parameters.AddWithValue("key", notice.DedupeKey);
            insertDeliveries.Parameters.AddWithValue("kind", notice.Kind);
            insertDeliveries.Parameters.AddWithValue("pending", DeliveryStatuses.Pending);
            insertDeliveries.Parameters.AddWithValue("now", now);
            insertDeliveries.Parameters.Add(new NpgsqlParameter<Guid[]>("users", [.. deliveries.Select(d => d.UserId)]));
            insertDeliveries.Parameters.Add(new NpgsqlParameter<string[]>("channels", [.. deliveries.Select(d => ChannelName(d.Channel))]));
            await insertDeliveries.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task ForgetAsync(Guid userId, CancellationToken ct)
    {
        await using var command = Command(
            await OpenAsync(ct),
            """
            delete from notifications.push_subscriptions where user_id = @user;
            delete from notifications.preferences where user_id = @user;
            delete from notifications.deliveries where user_id = @user;
            """);
        command.Parameters.AddWithValue("user", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<NotificationsExport?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var preferences = await db.Preferences.AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => new ExportedPreferences(row.NewShowPush, row.DailyDigestEmail, row.ReminderPush, row.ReminderEmail))
            .SingleOrDefaultAsync(ct);
        var devices = (await db.PushSubscriptions.AsNoTracking()
                .Where(row => row.UserId == userId)
                .OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.Endpoint, row.CreatedAtUtc, row.LastSuccessAtUtc })
                .ToListAsync(ct))
            .Select(row => new ExportedDevice(new Uri(row.Endpoint).Host, row.CreatedAtUtc, row.LastSuccessAtUtc))
            .ToList();
        var deliveries = await db.Deliveries.AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderBy(row => row.Id)
            .Select(row => new ExportedDelivery(row.Kind, row.Channel, row.Status, row.CreatedAtUtc))
            .ToListAsync(ct);
        return preferences is null && devices.Count == 0 && deliveries.Count == 0
            ? null
            : new NotificationsExport(preferences, devices, deliveries);
    }

    /// <summary>O nome do canal em <c>deliveries.channel</c>.</summary>
    internal static string ChannelName(NotificationChannel channel) =>
        channel == NotificationChannel.WebPush ? DeliveryChannels.Push : DeliveryChannels.Email;

    internal static NotificationChannel ChannelOf(string name) =>
        name == DeliveryChannels.Push ? NotificationChannel.WebPush : NotificationChannel.Email;

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        return (NpgsqlConnection)db.Database.GetDbConnection();
    }

    private static NpgsqlCommand Command(DbConnection connection, string sql) =>
#pragma warning disable CA2100 // SQL fixo deste arquivo; os valores vão sempre por parâmetro
        new(sql, (NpgsqlConnection)connection);
#pragma warning restore CA2100
}
