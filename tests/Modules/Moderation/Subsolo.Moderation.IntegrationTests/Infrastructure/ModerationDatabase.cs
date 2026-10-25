using Npgsql;

namespace Subsolo.Moderation.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0033 — leitura direta das tabelas do §6: <c>moderation.reports</c> e a outbox do Moderation, a coluna
/// <c>events.shows.hidden_at_utc</c> e a inbox do Events (o consumidor de <c>ShowHiddenByModeration</c>), e a cópia de
/// shows em volume. Antes da migração (tabela ou coluna ainda inexistente) as leituras devolvem vazio/nulo, para o teste
/// falhar na asserção.
/// </summary>
internal static class ModerationDatabase
{
    /// <summary>As linhas de <c>moderation.reports</c> (vazio se a tabela ainda não existe).</summary>
    public static async Task<IReadOnlyList<ReportRow>> ReportsAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        if (!await ExistsAsync(connection, "select to_regclass('moderation.reports') is not null", ct))
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            """
            select id, show_id, reporter_user_id, reason, details, status, created_at_utc, decided_at_utc, decided_by
            from moderation.reports order by created_at_utc, id
            """,
            connection);
        var rows = new List<ReportRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ReportRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                await reader.IsDBNullAsync(2, ct) ? null : reader.GetGuid(2),
                reader.GetString(3),
                await reader.IsDBNullAsync(4, ct) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                await reader.IsDBNullAsync(7, ct) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                await reader.IsDBNullAsync(8, ct) ? null : reader.GetGuid(8)));
        }

        return rows;
    }

    /// <summary>
    /// SPEC-0033 (Emenda 2) — <c>moderation.reports.decision_note</c> das denúncias do show (vazio se a tabela ou a coluna
    /// ainda não existem).
    /// </summary>
    public static async Task<IReadOnlyList<string?>> DecisionNotesAsync(string connectionString, Guid showId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        if (!await ExistsAsync(
                connection,
                """
                select exists (select 1 from information_schema.columns
                               where table_schema = 'moderation' and table_name = 'reports' and column_name = 'decision_note')
                """,
                ct))
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            "select decision_note from moderation.reports where show_id = @show order by created_at_utc, id", connection);
        command.Parameters.AddWithValue("show", showId);
        var notes = new List<string?>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            notes.Add(await reader.IsDBNullAsync(0, ct) ? null : reader.GetString(0));
        }

        return notes;
    }

    /// <summary><c>events.shows.hidden_at_utc</c> do show (nulo se visível ou se a coluna ainda não existe).</summary>
    public static async Task<DateTimeOffset?> HiddenAtAsync(string connectionString, Guid showId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        if (!await ExistsAsync(
                connection,
                """
                select exists (select 1 from information_schema.columns
                               where table_schema = 'events' and table_name = 'shows' and column_name = 'hidden_at_utc')
                """,
                ct))
        {
            return null;
        }

        await using var command = new NpgsqlCommand("select hidden_at_utc from events.shows where id = @id", connection);
        command.Parameters.AddWithValue("id", showId);
        var value = await command.ExecuteScalarAsync(ct);
        return value is DateTime utc
            ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
            : value as DateTimeOffset?;
    }

    /// <summary>O <c>content</c> (JSON) das mensagens da outbox do Moderation cujo tipo contém <paramref name="typeFragment"/>.</summary>
    public static async Task<IReadOnlyList<string>> OutboxContentsAsync(string connectionString, string typeFragment, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        if (!await ExistsAsync(connection, "select to_regclass('moderation.outbox_messages') is not null", ct))
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            "select content::text from moderation.outbox_messages where type like '%' || @fragment || '%' order by occurred_on_utc",
            connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        var contents = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            contents.Add(reader.GetString(0));
        }

        return contents;
    }

    /// <summary>As mensagens da inbox do Events cujo tipo contém <paramref name="typeFragment"/>.</summary>
    public static async Task<IReadOnlyList<InboxRow>> EventsInboxAsync(string connectionString, string typeFragment, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            select id, processed_on_utc, error from events.inbox_messages
            where type like '%' || @fragment || '%'
            order by occurred_on_utc
            """,
            connection);
        command.Parameters.AddWithValue("fragment", typeFragment);
        var rows = new List<InboxRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InboxRow(
                reader.GetGuid(0),
                await reader.IsDBNullAsync(1, ct) ? null : reader.GetFieldValue<DateTime>(1),
                await reader.IsDBNullAsync(2, ct) ? null : reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>
    /// Entrega de novo à inbox do Events a mensagem <paramref name="messageId"/> (mesmo tipo e conteúdo, id novo e ainda
    /// não processada), como um evento repetido pelo barramento; devolve o id novo.
    /// </summary>
    public static async Task<Guid> RedeliverToEventsAsync(string connectionString, Guid messageId, CancellationToken ct)
    {
        var copy = Guid.CreateVersion7();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            insert into events.inbox_messages (id, type, content, occurred_on_utc, processed_on_utc, attempts, next_attempt_on_utc, error)
            select @copy, type, content, occurred_on_utc, null, 0, null, null
            from events.inbox_messages where id = @id
            """,
            connection);
        command.Parameters.AddWithValue("copy", copy);
        command.Parameters.AddWithValue("id", messageId);
        (await command.ExecuteNonQueryAsync(ct)).ShouldBe(1, $"mensagem {messageId} não está na inbox do Events");
        return copy;
    }

    /// <summary>
    /// SPEC-0037 (H33-05) — grava direto em <c>moderation.reports</c> uma denúncia <c>pending</c> do show, como a que foi
    /// aceita entre a decisão de ocultar e o consumidor do Events; devolve o id.
    /// </summary>
    public static async Task<Guid> InsertPendingReportAsync(
        string connectionString, Guid showId, Guid reporterUserId, string reason, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            insert into moderation.reports (id, show_id, reporter_user_id, reason, details, status, created_at_utc)
            values (@id, @show, @reporter, @reason, null, 'pending', now())
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("show", showId);
        command.Parameters.AddWithValue("reporter", reporterUserId);
        command.Parameters.AddWithValue("reason", reason);
        (await command.ExecuteNonQueryAsync(ct)).ShouldBe(1, "a denúncia tardia não foi gravada");
        return id;
    }

    /// <summary>
    /// SPEC-0037 (H33-05, Emenda 1) — marca direto em <c>moderation.reports</c> as denúncias pendentes do show como
    /// <c>hidden</c> (decididas por <paramref name="adminId"/>), sem mensagem na outbox: o Moderation já decidiu ocultar,
    /// mas o Events ainda não soube (o show segue visível). Devolve quantas linhas mudaram.
    /// </summary>
    public static async Task<int> MarkReportsHiddenAsync(string connectionString, Guid showId, Guid adminId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            update moderation.reports set status = 'hidden', decided_at_utc = now(), decided_by = @admin
            where show_id = @show and status = 'pending'
            """,
            connection);
        command.Parameters.AddWithValue("show", showId);
        command.Parameters.AddWithValue("admin", adminId);
        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Copia <paramref name="copies"/> vezes o show <paramref name="templateId"/> em <c>events.shows</c> (id novo e slug com
    /// sufixo; o resto igual); devolve os ids das cópias (copiado do <c>ReminderRequests</c> da SPEC-0032).
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> CloneShowAsync(string connectionString, Guid templateId, int copies, CancellationToken ct)
    {
        await using var connection = await OpenAsync(connectionString, ct);
        await using var command = new NpgsqlCommand(
            """
            insert into events.shows
            select (jsonb_populate_record(
                null::events.shows,
                to_jsonb(s) || jsonb_build_object('id', gen_random_uuid(), 'slug', s.slug || '-c' || n))).*
            from events.shows s, generate_series(1, @copies) as n
            where s.id = @template
            returning id
            """,
            connection);
        command.Parameters.AddWithValue("copies", copies);
        command.Parameters.AddWithValue("template", templateId);
        var ids = new List<Guid>(copies);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }

        ids.Count.ShouldBe(copies, $"o show {templateId} não foi copiado");
        return ids;
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

#pragma warning disable CA2100 // SQL fixo dos chamadores deste arquivo
    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }
#pragma warning restore CA2100
}

/// <summary>SPEC-0033 — uma linha de <c>moderation.reports</c> (§6).</summary>
internal sealed record ReportRow(
    Guid Id,
    Guid ShowId,
    Guid? ReporterUserId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    Guid? DecidedBy);

/// <summary>Uma mensagem da inbox: id, processada em e erro.</summary>
internal sealed record InboxRow(Guid Id, DateTime? ProcessedOnUtc, string? Error);
