using System.Globalization;
using Npgsql;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0031 H30-02 (Emenda 1, contrato v2: job <c>NoticesRetention</c> diário) — com avisos e entregas gravados direto
/// em <c>notifications.notices</c>/<c>deliveries</c>: entregas concluídas (<c>sent</c>, <c>failed</c>,
/// <c>skipped</c>) com mais de 90 dias somem; uma entrega <c>pending</c> antiga fica, e o aviso dela também; avisos com
/// mais de 90 dias sem entrega pendente somem; as linhas recentes ficam. O job registra
/// <c>NoticesRetentionCompleted</c> com as contagens apagadas (<c>Deliveries</c> e <c>Notices</c>, Emenda 3). O job de
/// entregas fica pausado (a entrega pendente não é enviada).
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class NoticesRetentionTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0031:H30-02")]
    [Trait("Kind", "Integration")]
    public async Task OnlyOldFinishedDeliveriesAndOldNoticesWithoutPendingDeliveriesAreDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        await NoticeJobs.PauseAsync(api, NoticeJobs.Deliveries, ct);
        var now = DateTimeOffset.UtcNow;
        var old = now.AddDays(-91);
        var recent = now.AddDays(-1);

        var oldFinished = await NoticeAsync(api, "antigo-concluido", old, ct);
        var oldSent = await DeliveryAsync(api, oldFinished, "sent", old, ct);
        var oldFailed = await DeliveryAsync(api, oldFinished, "failed", old, ct);
        var oldSkipped = await DeliveryAsync(api, oldFinished, "skipped", old, ct);
        var oldWithPending = await NoticeAsync(api, "antigo-pendente", old, ct);
        var oldPending = await DeliveryAsync(api, oldWithPending, "pending", old, ct);
        var oldSentBesidePending = await DeliveryAsync(api, oldWithPending, "sent", old, ct);
        var oldEmpty = await NoticeAsync(api, "antigo-vazio", old, ct);
        var recentNotice = await NoticeAsync(api, "recente", recent, ct);
        var recentSent = await DeliveryAsync(api, recentNotice, "sent", recent, ct);
        var recentFailed = await DeliveryAsync(api, recentNotice, "failed", recent, ct);
        var runs = await NoticeJobs.RunsAsync(api, NoticeJobs.Retention, ct);

        await runs.RunOnceAsync(ct);

        var deliveries = await IdsAsync(api, "select id::text from notifications.deliveries", ct);
        deliveries.ShouldBe(
            [oldPending.ToString(CultureInfo.InvariantCulture), recentSent.ToString(CultureInfo.InvariantCulture), recentFailed.ToString(CultureInfo.InvariantCulture)],
            ignoreOrder: true,
            $"apagadas: sent {oldSent}, failed {oldFailed}, skipped {oldSkipped} e sent {oldSentBesidePending} (antigas); ficam a pendente e as recentes");
        var notices = await IdsAsync(api, "select id::text from notifications.notices", ct);
        notices.ShouldBe(
            [oldWithPending.ToString(), recentNotice.ToString()],
            ignoreOrder: true,
            $"apagados os avisos antigos sem pendência ({oldFinished}, {oldEmpty}); ficam o com entrega pendente e o recente");

        var completed = api.Logs.Where(log => log.EventId.Name == "NoticesRetentionCompleted").ToList();
        completed.ShouldNotBeEmpty("o job de retenção não registrou NoticesRetentionCompleted");
        completed.ShouldContain(
            log => IsCount(Attribute(log, "Deliveries"), 4) && IsCount(Attribute(log, "Notices"), 2),
            "NoticesRetentionCompleted sem Deliveries = 4 e Notices = 2");
    }

    private static object? Attribute(OpenTelemetry.Logs.LogRecord log, string name) =>
        (log.Attributes ?? []).FirstOrDefault(attribute => attribute.Key == name).Value;

    private static bool IsCount(object? value, long expected) =>
        value is int or long or short && Convert.ToInt64(value, CultureInfo.InvariantCulture) == expected;

    private static async Task<Guid> NoticeAsync(NotificationsApiFactory api, string key, DateTimeOffset createdAt, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            api,
            """
            insert into notifications.notices (id, dedupe_key, kind, title, body, url, created_at_utc)
            values (@id, @key, 'reminder', 'Amanhã: Podridão', 'Sábado, 21h', '/cidade-exemplo/podridao', @at)
            """,
            command =>
            {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("key", $"h30-02-{key}-{id:N}");
                command.Parameters.AddWithValue("at", createdAt);
            },
            ct);
        return id;
    }

    private static async Task<long> DeliveryAsync(
        NotificationsApiFactory api, Guid noticeId, string status, DateTimeOffset at, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            insert into notifications.deliveries
                (notice_id, user_id, dedupe_key, kind, channel, status, attempts, created_at_utc, next_attempt_at_utc, completed_at_utc)
            select n.id, @user, n.dedupe_key, n.kind, 'email', @status, 1, @at, @next, @completed
            from notifications.notices n where n.id = @notice
            returning id
            """,
            connection);
        command.Parameters.AddWithValue("user", Guid.CreateVersion7());
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("at", at);
        command.Parameters.AddWithValue("next", status == "pending" ? DateTimeOffset.UtcNow.AddDays(1) : at);
        command.Parameters.AddWithValue("completed", status == "pending" ? DBNull.Value : at);
        command.Parameters.AddWithValue("notice", noticeId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(
        NotificationsApiFactory api, string sql, Action<NpgsqlCommand> parameters, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // SQL fixo deste teste, com parâmetros
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        parameters(command);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<string>> IdsAsync(NotificationsApiFactory api, string sql, CancellationToken ct)
    {
        var ids = new List<string>();
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // SQL fixo deste teste
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }
}
