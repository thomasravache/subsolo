namespace Subsolo.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// SPEC-0030 — <c>notifications.push_subscriptions</c> (§6): um navegador inscrito (endpoint único) com as chaves dele;
/// <see cref="LastSuccessAtUtc"/> = último push aceito pelo serviço.
/// </summary>
internal sealed class PushSubscriptionRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastSuccessAtUtc { get; set; }
}

/// <summary>SPEC-0030 — <c>notifications.preferences</c> (§6): os quatro interruptores de um usuário.</summary>
internal sealed class PreferencesRow
{
    public Guid UserId { get; set; }

    public bool NewShowPush { get; set; }

    public bool DailyDigestEmail { get; set; }

    public bool ReminderPush { get; set; }

    public bool ReminderEmail { get; set; }
}

/// <summary>
/// SPEC-0030 — <c>notifications.notices</c>: o conteúdo de um aviso enfileirado (tipo, chave, título, corpo e URL), uma
/// linha por chamada de <c>EnqueueAsync</c>, lido pelas entregas.
/// </summary>
internal sealed class NoticeRow
{
    public Guid Id { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>
/// SPEC-0030 — <c>notifications.deliveries</c> (§6): a entrega de um aviso a um usuário por um canal (<c>push</c> |
/// <c>email</c>), única por (usuário, chave, canal) — é a fila de saída do módulo: <c>pending</c> até o envio
/// (<see cref="NextAttemptAtUtc"/>, <see cref="Attempts"/>, e os aparelhos que ainda faltam em
/// <see cref="PendingEndpoints"/>), depois <c>sent</c> ou <c>failed</c>.
/// </summary>
internal sealed class DeliveryRow
{
    public long Id { get; set; }

    public Guid NoticeId { get; set; }

    public Guid UserId { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset NextAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public List<string>? PendingEndpoints { get; set; }
}
