using System.Globalization;
using System.Net;
using Npgsql;
using NpgsqlTypes;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0032 — preparação do lembrete na véspera: o show cadastrado (e adiado) pelo admin na cidade de desenvolvimento,
/// o "vou" e as preferências de lembrete do fã (SPEC-0030), a carga direta das preferências e dos shows em volume, e as
/// leituras do §6 (<c>engagement.digest_runs</c>, Emenda 2). Os horários são de São Paulo (fuso da cidade de
/// desenvolvimento).
/// </summary>
internal static class ReminderRequests
{
    /// <summary>Expressão cron do Quartz que nunca dispara no teste (o job só roda quando o teste dispara).</summary>
    public const string NeverCron = "0 0 0 1 1 ? 2099";

    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>O lembrete só roda quando o teste dispara (<c>Engagement:ReminderCron</c> nunca dispara sozinho).</summary>
    public static Dictionary<string, string> ManualReminderSettings() =>
        new(StringComparer.Ordinal) { ["Engagement:ReminderCron"] = NeverCron };

    /// <summary>
    /// O admin cadastra um show agendado na cidade de desenvolvimento, <paramref name="days"/> dias depois de
    /// <paramref name="now"/> às 22h (São Paulo), com um produtor sem dono, a casa <see cref="NoticeRequests.Venue"/> e
    /// "Cova Rasa" no line-up; devolve o que a edição precisa reenviar.
    /// </summary>
    public static async Task<ReminderShow> CreateReminderShowAsync(
        this EngagementApiClient admin, string title, DateTimeOffset now, int days, CancellationToken ct)
    {
        var producer = await admin.CreateApprovedProfileAsync($"Produtora {title}", ct, type: "producer");
        var flyerId = await admin.FlyerAsync(ct);
        var startsLocal = DateTime.ParseExact(AttendanceRequests.LocalAt(now, days), "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        using var response = await admin.PostAsync("/api/v1/shows", Body(producer, title, startsLocal, flyerId), ct);
        var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        return new ReminderShow(
            created.GetProperty("id").GetGuid(),
            created.GetProperty("slug").GetString().ShouldNotBeNull(),
            title,
            producer,
            flyerId,
            startsLocal);
    }

    /// <summary>O admin muda a data do show para <paramref name="startsLocal"/> (<c>PUT /api/v1/shows/{id}</c>, 200).</summary>
    public static async Task<ReminderShow> RescheduleAsync(
        this EngagementApiClient admin, ReminderShow show, DateTime startsLocal, CancellationToken ct)
    {
        using var response = await admin.MutateAsync(
            HttpMethod.Put, $"/api/v1/shows/{show.Id}", Body(show.OrganizerProfileId, show.Title, startsLocal, show.FlyerId), ct);
        await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return show with { StartsLocal = startsLocal };
    }

    /// <summary>O fã marca "vou" (<c>PUT /me/attendances/{showId}</c>).</summary>
    public static Task AttendAsync(this EngagementApiClient fan, Guid showId, CancellationToken ct) =>
        fan.PutAsync(AttendanceRequests.Attend(showId), ct).ShouldSucceedAsync(ct);

    /// <summary><c>PUT /me/notification-preferences</c> só com o lembrete (push e/ou e-mail) ligado (200).</summary>
    public static async Task SetReminderPreferencesAsync(
        this EngagementApiClient fan, bool reminderPush, bool reminderEmail, CancellationToken ct)
    {
        using var response = await fan.MutateAsync(
            HttpMethod.Put,
            NoticeRequests.Preferences,
            new { newShowPush = false, dailyDigestEmail = false, reminderPush, reminderEmail },
            ct);
        await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }

    /// <summary>O instante (UTC) da véspera do show às <paramref name="hour"/>:<paramref name="minute"/> de São Paulo.</summary>
    public static DateTimeOffset EveAt(DateTime startsLocal, int hour, int minute) =>
        LocalToUtc(startsLocal.Date.AddDays(-1).AddHours(hour).AddMinutes(minute));

    /// <summary>Um horário local de São Paulo em UTC.</summary>
    public static DateTimeOffset LocalToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), SaoPaulo), TimeSpan.Zero);

    /// <summary>A data de hoje em São Paulo no instante <paramref name="utc"/>.</summary>
    public static DateTime LocalDate(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, SaoPaulo).Date;

    /// <summary>A chave do §6: <c>reminder:{ShowId}:{StartsAtUtc yyyyMMddHHmm}</c>.</summary>
    public static string ReminderKey(Guid showId, DateTime startsLocal) =>
        string.Create(CultureInfo.InvariantCulture, $"reminder:{showId}:{LocalToUtc(startsLocal).UtcDateTime:yyyyMMddHHmm}");

    /// <summary>
    /// Copia <paramref name="copies"/> vezes o show <paramref name="templateId"/> em <c>events.shows</c> (id novo e slug com
    /// sufixo; o resto igual, inclusive início e situação); devolve os ids das cópias.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> CloneShowAsync(
        string connectionString, Guid templateId, int copies, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
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

    /// <summary>Grava (COPY binário) as preferências de cada usuário só com o lembrete por e-mail e/ou push.</summary>
    public static async Task SeedReminderPreferencesAsync(
        string connectionString, IEnumerable<Guid> userIds, bool reminderPush, bool reminderEmail, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var writer = await connection.BeginBinaryImportAsync(
            "copy notifications.preferences (user_id, new_show_push, daily_digest_email, reminder_push, reminder_email) from stdin (format binary)", ct);
        foreach (var userId in userIds)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(userId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(reminderPush, NpgsqlDbType.Boolean, ct);
            await writer.WriteAsync(reminderEmail, NpgsqlDbType.Boolean, ct);
        }

        await writer.CompleteAsync(ct);
    }

    /// <summary>
    /// As linhas de <c>engagement.digest_runs(digest_date date PK, completed_at_utc timestamptz)</c> (§6, Emenda 2); vazio
    /// se a tabela ainda não existe.
    /// </summary>
    public static async Task<IReadOnlyList<DigestRunRow>> DigestRunsAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using (var exists = new NpgsqlCommand("select to_regclass('engagement.digest_runs') is not null", connection))
        {
            if (!(bool)(await exists.ExecuteScalarAsync(ct))!)
            {
                return [];
            }
        }

        await using var command = new NpgsqlCommand(
            "select digest_date, completed_at_utc from engagement.digest_runs order by digest_date", connection);
        var rows = new List<DigestRunRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new DigestRunRow(
                reader.GetFieldValue<DateOnly>(0),
                await reader.IsDBNullAsync(1, ct) ? null : reader.GetFieldValue<DateTimeOffset>(1)));
        }

        return rows;
    }

    private static Dictionary<string, object?> Body(Guid organizer, string title, DateTime startsLocal, string flyerId) =>
        new(StringComparer.Ordinal)
        {
            ["organizerProfileId"] = organizer,
            ["title"] = title,
            ["city"] = EngagementApiFactory.City,
            ["startsLocal"] = startsLocal.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            ["venue"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = NoticeRequests.Venue },
            ["lineup"] = new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Cova Rasa" } },
            ["styles"] = new[] { EngagementApiFactory.Style },
            ["price"] = "R$ 30",
            ["ticketUrl"] = null,
            ["flyerId"] = flyerId,
        };
}

/// <summary>SPEC-0032 — um show cadastrado para o lembrete: o que a edição reenvia e o início local (São Paulo).</summary>
internal sealed record ReminderShow(Guid Id, string Slug, string Title, Guid OrganizerProfileId, string FlyerId, DateTime StartsLocal)
{
    /// <summary>A chave do lembrete deste show na data atual dele.</summary>
    public string Key => ReminderRequests.ReminderKey(Id, StartsLocal);
}

/// <summary>SPEC-0032 — uma linha de <c>engagement.digest_runs</c>.</summary>
internal sealed record DigestRunRow(DateOnly DigestDate, DateTimeOffset? CompletedAtUtc);
