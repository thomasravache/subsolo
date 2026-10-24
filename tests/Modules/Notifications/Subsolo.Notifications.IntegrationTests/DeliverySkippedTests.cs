using Npgsql;
using NpgsqlTypes;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0031 H30-06 — uma entrega cujo destinatário não tem mais o canal (o aparelho foi removido; o usuário foi
/// excluído e não há e-mail) termina, quando o job de entregas a processa, com o evento <c>NoticeDeliverySkipped</c> — não
/// <c>NoticeDeliveryFailed</c>, que é a base do alerta "Falha no envio de aviso" — e sai da fila (não fica
/// <c>pending</c>). O job de entregas fica pausado até o canal sumir e é disparado pelo teste.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class DeliverySkippedTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const string Skipped = "NoticeDeliverySkipped";

    private const string Failed = "NoticeDeliveryFailed";

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-06")]
    [Trait("Kind", "Integration")]
    public async Task APushDeliveryWithoutDevicesIsSkippedNotFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        var fan = Guid.CreateVersion7();
        await NotificationsDatabase.SeedPreferencesAsync(
            api.ConnectionString, [(fan, NewShowPush: false, DailyDigestEmail: false, ReminderPush: true, ReminderEmail: false)], ct);
        await NotificationsDatabase.SeedSubscriptionsAsync(
            api.ConnectionString, [(fan, NotificationsRequests.FcmEndpoint("h30-06"), "p256dh-h30-06", "auth-h30-06")], ct);
        await NoticeJobs.PauseAsync(api, NoticeJobs.Deliveries, ct);
        await EnqueueReminderAsync(api, fan, ct);
        await ExecuteAsync(api, "delete from notifications.push_subscriptions where user_id = @user", fan, ct);
        var runs = await NoticeJobs.RunsAsync(api, NoticeJobs.Deliveries, ct);

        await runs.RunOnceAsync(ct);

        await ShouldBeSkippedAsync(api, fan, "push", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-06")]
    [Trait("Kind", "Integration")]
    public async Task AnEmailDeliveryToADeletedUserIsSkippedNotFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        var fan = Guid.CreateVersion7();
        await SeedUserAsync(api, fan, $"fa.excluido-{fan:N}@subsolo.test", ct);
        await NotificationsDatabase.SeedPreferencesAsync(
            api.ConnectionString, [(fan, NewShowPush: false, DailyDigestEmail: false, ReminderPush: false, ReminderEmail: true)], ct);
        await NoticeJobs.PauseAsync(api, NoticeJobs.Deliveries, ct);
        await EnqueueReminderAsync(api, fan, ct);
        await ExecuteAsync(api, "delete from users.users where id = @user", fan, ct);
        var runs = await NoticeJobs.RunsAsync(api, NoticeJobs.Deliveries, ct);

        await runs.RunOnceAsync(ct);

        await ShouldBeSkippedAsync(api, fan, "email", ct);
    }

    private static async Task ShouldBeSkippedAsync(NotificationsApiFactory api, Guid fan, string channel, CancellationToken ct)
    {
        var delivery = (await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct))
            .Single(row => row.UserId == fan && row.Channel == channel);
        delivery.Status.ShouldNotBe("pending", "a entrega sem canal ficou na fila");
        var events = api.Logs.Select(log => log.EventId.Name).Where(name => name is Skipped or Failed).ToList();
        events.ShouldContain(Skipped, $"a entrega {channel} sem canal não registrou {Skipped}");
        events.ShouldNotContain(Failed, $"a entrega {channel} sem canal registrou {Failed} (entra no alerta \"Falha no envio de aviso\")");
    }

    private static async Task EnqueueReminderAsync(NotificationsApiFactory api, Guid fan, CancellationToken ct)
    {
        var enqueued = await api.EnqueueAsync(
            [fan],
            new Notice("reminder", $"h30-06-{Guid.NewGuid():N}", "Amanhã: Podridão no Porão", "Sábado, 21h", "https://localhost/cidade-exemplo/podridao"),
            ct);
        enqueued.IsSuccess.ShouldBeTrue(enqueued.IsFailure ? $"{enqueued.Error.Code} — {enqueued.Error.Description}" : null);
        (await NotificationsDatabase.DeliveryStatesAsync(api.ConnectionString, ct)).Count(row => row.UserId == fan)
            .ShouldBe(1, "o aviso não criou a entrega");
    }

    private static async Task SeedUserAsync(NotificationsApiFactory api, Guid id, string email, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "insert into users.users (id, email, display_name, roles, created_at_utc) values (@id, @email, null, @roles, now())",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.Add(new NpgsqlParameter("roles", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = Array.Empty<string>() });
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteAsync(NotificationsApiFactory api, string sql, Guid user, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // SQL fixo deste teste, com parâmetro
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        command.Parameters.AddWithValue("user", user);
        (await command.ExecuteNonQueryAsync(ct)).ShouldBe(1, sql);
    }
}
