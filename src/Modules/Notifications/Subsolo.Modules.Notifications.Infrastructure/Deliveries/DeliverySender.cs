using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Diagnostics;
using Subsolo.Modules.Notifications.Application.Features.Unsubscribe;
using Subsolo.Modules.Notifications.Application.Templates;
using Subsolo.Modules.Notifications.Domain.Notices;
using Subsolo.Modules.Notifications.Infrastructure.Persistence;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Infrastructure.Deliveries;

/// <summary>
/// SPEC-0030 — envia um lote das entregas pendentes (a fila de saída do módulo, <c>notifications.deliveries</c>):
/// <list type="bullet">
/// <item>push: o aviso <c>{title, body, url}</c> vai a cada aparelho inscrito do usuário (na nova tentativa, só aos que
/// faltaram); 404/410 removem a inscrição; 429/5xx/rede ficam para a próxima tentativa, com espera crescente;</item>
/// <item>e-mail: o endereço vem do <see cref="IRecipientDirectory"/> (Users) na hora do envio, com o link de descadastro
/// daquele tipo; SMTP fora do ar → nova tentativa.</item>
/// </list>
/// Depois de <see cref="MaxAttempts"/> tentativas a entrega desiste (<c>failed</c>, com o evento
/// <c>NoticeDeliveryFailed</c>, base do alerta "Falha no envio de aviso"). Uma entrega aceita nunca é enviada de novo
/// (§5, IT-08): uma exceção inesperada num envio vira nova tentativa só daquela entrega, e o estado do que já foi
/// tentado é gravado numa transação depois dos envios — também quando o lote é cancelado (interrupção do job).
/// </summary>
internal sealed partial class DeliverySender(
    NotificationsDbContext db,
    IEnumerable<INotificationChannel> channels,
    IRecipientDirectory directory,
    IUnsubscribeTokens tokens,
    IOptions<NoticeLinksOptions> links,
    TimeProvider clock,
    ILogger<DeliverySender> logger)
{
    /// <summary>Entregas lidas por lote.</summary>
    public const int BatchSize = 500;

    /// <summary>Envios simultâneos.</summary>
    public const int Parallelism = 32;

    /// <summary>Tentativas de cada entrega (a espera dobra a cada uma: 2, 4, 8, 16 e 32 s).</summary>
    public const int MaxAttempts = 6;

    private readonly INotificationChannel _push = channels.Single(channel => channel.Channel == NotificationChannel.WebPush);
    private readonly INotificationChannel _email = channels.Single(channel => channel.Channel == NotificationChannel.Email);

    /// <summary>Espera antes da tentativa seguinte à de número <paramref name="attempts"/>.</summary>
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(attempts, 1, 10)));

    /// <summary>Envia um lote de entregas vencidas; devolve quantas foram processadas.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var connection = await OpenAsync(ct);
        var due = await DueAsync(connection, clock.GetUtcNow(), ct);
        if (due.Count == 0)
        {
            return 0;
        }

        var devices = await DevicesAsync(connection, [.. due.Where(d => d.Channel == DeliveryChannels.Push).Select(d => d.UserId).Distinct()], ct);
        var emailUsers = due.Where(d => d.Channel == DeliveryChannels.Email).Select(d => d.UserId).Distinct().ToList();
        var emails = emailUsers.Count > 0 ? await directory.GetEmailsAsync(emailUsers, ct) : new Dictionary<Guid, string>();

        var outcomes = new ConcurrentBag<Outcome>();
        try
        {
            await Parallel.ForEachAsync(
                due,
                new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
                async (delivery, token) =>
                {
                    if (await SendSafelyAsync(delivery, devices, emails, token) is { } outcome)
                    {
                        outcomes.Add(outcome);
                    }
                });
        }
        finally
        {
            // Grava o que já foi tentado mesmo com o lote cancelado: o que saiu não pode sair de novo (§5).
            var finished = outcomes.ToList();
            if (finished.Count > 0)
            {
                var now = clock.GetUtcNow();
                await SaveAsync(connection, finished, now, CancellationToken.None);
                foreach (var outcome in finished.Where(outcome => FinalStatus(outcome) == DeliveryStatuses.Failed))
                {
                    LogDeliveryFailed(logger, outcome.Delivery.Id, outcome.Delivery.Channel, outcome.Delivery.Kind, outcome.Delivery.Attempts + 1);
                }
            }
        }

        return due.Count;
    }

    /// <summary>
    /// Envia uma entrega; uma exceção inesperada do canal vira nova tentativa só dela. Com o lote cancelado, a entrega
    /// em envio fica como estava (<see langword="null"/>: pendente, sem contar a tentativa) e sai no lote seguinte.
    /// </summary>
    private async Task<Outcome?> SendSafelyAsync(
        Due delivery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Device>> devices,
        IReadOnlyDictionary<Guid, string> emails,
        CancellationToken ct)
    {
        try
        {
            return delivery.Channel == DeliveryChannels.Push
                ? await SendPushAsync(delivery, devices.GetValueOrDefault(delivery.UserId) ?? [], ct)
                : await SendEmailAsync(delivery, emails.GetValueOrDefault(delivery.UserId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
#pragma warning disable CA1031 // Uma falha inesperada de um envio não pode descartar o resultado das outras do lote.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogUnexpectedFailure(logger, exception, delivery.Id, delivery.Channel);
            NotificationsTelemetry.Delivered(delivery.Channel, "retry");
            return Outcome.Retry(delivery, delivery.PendingEndpoints);
        }
    }

    private async Task<Outcome> SendPushAsync(Due delivery, IReadOnlyList<Device> devices, CancellationToken ct)
    {
        var targets = delivery.PendingEndpoints is { } pending
            ? devices.Where(device => pending.Contains(device.Endpoint, StringComparer.Ordinal)).ToList()
            : devices;
        if (targets.Count == 0)
        {
            NotificationsTelemetry.Delivered(DeliveryChannels.Push, DeliveryStatuses.Failed);
            return Outcome.Done(delivery, DeliveryStatuses.Failed);
        }

        var payload = PushPayload.Serialize(delivery.Title, delivery.Body, delivery.Url);
        var sent = new List<string>();
        var gone = new List<string>();
        var retry = new List<string>();
        for (var index = 0; index < targets.Count; index++)
        {
            var device = targets[index];
            string code;
            try
            {
                var result = await _push.SendAsync(new PushNotification(device.Endpoint, device.P256dh, device.Auth, payload), ct);
                code = result.IsSuccess ? DeliveryStatuses.Sent : result.Error.Code;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested && sent.Count + gone.Count > 0)
            {
                // Lote cancelado depois de algum aparelho aceitar: grava os que já receberam; o resto fica para depois.
                retry.AddRange(targets.Skip(index).Select(target => target.Endpoint));
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Falha inesperada num aparelho: só ele fica para a nova tentativa (o endpoint não vai para o log).
                LogUnexpectedFailure(logger, exception, delivery.Id, DeliveryChannels.Push);
                code = PushFailures.Temporary;
            }

            switch (code)
            {
                case DeliveryStatuses.Sent:
                    sent.Add(device.Endpoint);
                    break;
                case PushFailures.Gone:
                    gone.Add(device.Endpoint);
                    NotificationsTelemetry.SubscriptionRemoved();
                    break;
                case PushFailures.Temporary:
                    retry.Add(device.Endpoint);
                    break;
            }

            NotificationsTelemetry.Delivered(DeliveryChannels.Push, ResultName(code));
        }

        var outcome = retry.Count > 0
            ? Outcome.Retry(delivery, retry)
            : Outcome.Done(delivery, sent.Count > 0 ? DeliveryStatuses.Sent : DeliveryStatuses.Failed);
        return outcome with { Succeeded = sent, Gone = gone };
    }

    private async Task<Outcome> SendEmailAsync(Due delivery, string? email, CancellationToken ct)
    {
        var token = tokens.Create(delivery.UserId, delivery.Kind);
        if (email is null || token is null || !NoticeKinds.HasEmail(delivery.Kind))
        {
            // Usuário excluído, sem chave de descadastro configurada ou tipo sem e-mail: não há como enviar.
            if (token is null)
            {
                LogNoUnsubscribeKey(logger);
            }

            NotificationsTelemetry.Delivered(DeliveryChannels.Email, DeliveryStatuses.Failed);
            return Outcome.Done(delivery, DeliveryStatuses.Failed);
        }

        var content = NoticeEmailTemplate.Render(
            delivery.Kind, delivery.Title, delivery.Body, delivery.Url, links.Value.UnsubscribeLink(token));
        var result = await _email.SendAsync(
            new EmailNotification(email, content.Subject, content.HtmlBody, content.TextBody), ct);
        NotificationsTelemetry.Delivered(DeliveryChannels.Email, result.IsSuccess ? DeliveryStatuses.Sent : "retry");
        return result.IsSuccess ? Outcome.Done(delivery, DeliveryStatuses.Sent) : Outcome.Retry(delivery, null);
    }

    private static string ResultName(string code) => code switch
    {
        DeliveryStatuses.Sent => DeliveryStatuses.Sent,
        PushFailures.Gone => "gone",
        PushFailures.Temporary => "retry",
        _ => DeliveryStatuses.Failed,
    };

    private static async Task<List<Due>> DueAsync(NpgsqlConnection connection, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            select d.id, d.user_id, d.kind, d.channel, d.attempts, d.pending_endpoints, n.title, n.body, n.url
            from notifications.deliveries d
            join notifications.notices n on n.id = d.notice_id
            where d.status = @pending and d.next_attempt_at_utc <= @now
            order by d.next_attempt_at_utc, d.id
            limit @limit
            """,
            connection);
        command.Parameters.AddWithValue("pending", DeliveryStatuses.Pending);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("limit", BatchSize);
        var due = new List<Due>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            due.Add(new Due(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<string[]>(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8)));
        }

        return due;
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<Device>>> DevicesAsync(
        NpgsqlConnection connection, Guid[] userIds, CancellationToken ct)
    {
        var devices = new Dictionary<Guid, List<Device>>();
        if (userIds.Length > 0)
        {
            await using var command = new NpgsqlCommand(
                "select user_id, endpoint, p256dh, auth from notifications.push_subscriptions where user_id = any(@users)",
                connection);
            command.Parameters.Add(new NpgsqlParameter<Guid[]>("users", userIds));
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var userId = reader.GetGuid(0);
                if (!devices.TryGetValue(userId, out var list))
                {
                    devices[userId] = list = [];
                }

                list.Add(new Device(reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
        }

        return devices.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Device>)pair.Value);
    }

    /// <summary>O estado gravado da entrega: o do envio ou, esgotadas as tentativas, <c>sent</c> | <c>failed</c>.</summary>
    private static string FinalStatus(Outcome outcome)
    {
        var givingUp = outcome.Status == DeliveryStatuses.Pending && outcome.Delivery.Attempts + 1 >= MaxAttempts;
        return givingUp
            ? (outcome.Succeeded.Count > 0 ? DeliveryStatuses.Sent : DeliveryStatuses.Failed)
            : outcome.Status;
    }

    private static async Task SaveAsync(NpgsqlConnection connection, IReadOnlyList<Outcome> outcomes, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var batch = new NpgsqlBatch(connection, transaction);
        foreach (var outcome in outcomes)
        {
            var attempts = outcome.Delivery.Attempts + 1;
            var status = FinalStatus(outcome);
            var command = new NpgsqlBatchCommand(
                """
                update notifications.deliveries
                set status = @status, attempts = @attempts, next_attempt_at_utc = @next, completed_at_utc = @completed,
                    pending_endpoints = @pendingEndpoints
                where id = @id
                """);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("attempts", attempts);
            command.Parameters.AddWithValue("next", status == DeliveryStatuses.Pending ? now + Backoff(attempts) : now);
            command.Parameters.AddWithValue("completed", status == DeliveryStatuses.Pending ? DBNull.Value : now);
            command.Parameters.Add(new NpgsqlParameter<string[]?>(
                "pendingEndpoints",
                status == DeliveryStatuses.Pending ? outcome.RetryEndpoints?.ToArray() : null));
            command.Parameters.AddWithValue("id", outcome.Delivery.Id);
            batch.BatchCommands.Add(command);
        }

        var gone = outcomes.SelectMany(outcome => outcome.Gone).Distinct(StringComparer.Ordinal).ToArray();
        if (gone.Length > 0)
        {
            var remove = new NpgsqlBatchCommand("delete from notifications.push_subscriptions where endpoint = any(@endpoints)");
            remove.Parameters.Add(new NpgsqlParameter<string[]>("endpoints", gone));
            batch.BatchCommands.Add(remove);
        }

        var succeeded = outcomes.SelectMany(outcome => outcome.Succeeded).Distinct(StringComparer.Ordinal).ToArray();
        if (succeeded.Length > 0)
        {
            var touch = new NpgsqlBatchCommand(
                "update notifications.push_subscriptions set last_success_at_utc = @now where endpoint = any(@endpoints)");
            touch.Parameters.AddWithValue("now", now);
            touch.Parameters.Add(new NpgsqlParameter<string[]>("endpoints", succeeded));
            batch.BatchCommands.Add(touch);
        }

        await batch.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        return (NpgsqlConnection)db.Database.GetDbConnection();
    }

    [LoggerMessage(
        EventName = "UnsubscribeKeyMissing",
        Level = LogLevel.Error,
        Message = "E-mail de aviso não enviado: sem Notifications:Unsubscribe:SigningKey não há link de descadastro")]
    private static partial void LogNoUnsubscribeKey(ILogger logger);

    /// <summary>Um evento por entrega que termina como falha, em qualquer canal (§8, alerta "Falha no envio de aviso").</summary>
    [LoggerMessage(
        EventName = "NoticeDeliveryFailed",
        Level = LogLevel.Warning,
        Message = "Aviso não entregue: entrega {DeliveryId} ({Channel}, {Kind}) terminou como falha após {Attempts} tentativa(s)")]
    private static partial void LogDeliveryFailed(ILogger logger, long deliveryId, string channel, string kind, int attempts);

    [LoggerMessage(
        EventName = "NoticeSendError",
        Level = LogLevel.Error,
        Message = "Falha inesperada ao enviar a entrega {DeliveryId} ({Channel}); nova tentativa com espera")]
    private static partial void LogUnexpectedFailure(ILogger logger, Exception exception, long deliveryId, string channel);

    /// <summary>Uma entrega vencida, com o conteúdo do aviso.</summary>
    private sealed record Due(
        long Id,
        Guid UserId,
        string Kind,
        string Channel,
        int Attempts,
        string[]? PendingEndpoints,
        string Title,
        string Body,
        string Url);

    /// <summary>Um aparelho inscrito.</summary>
    private sealed record Device(string Endpoint, string P256dh, string Auth);

    /// <summary>
    /// O resultado de uma entrega no lote: concluída (<c>sent</c> | <c>failed</c>) ou pendente para nova tentativa
    /// (<see cref="RetryEndpoints"/>: os aparelhos que faltaram; <see langword="null"/> no e-mail), com os aparelhos que
    /// aceitaram e os que morreram (404/410).
    /// </summary>
    private sealed record Outcome(Due Delivery, string Status, IReadOnlyList<string>? RetryEndpoints)
    {
        public List<string> Succeeded { get; init; } = [];

        public List<string> Gone { get; init; } = [];

        public static Outcome Done(Due delivery, string status) => new(delivery, status, null);

        public static Outcome Retry(Due delivery, IReadOnlyList<string>? endpoints) =>
            new(delivery, DeliveryStatuses.Pending, endpoints);
    }
}
