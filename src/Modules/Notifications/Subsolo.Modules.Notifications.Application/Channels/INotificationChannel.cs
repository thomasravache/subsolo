using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Channels;

/// <summary>
/// SPEC-0018 (ADR-0014) — canal plugável de entrega: <see cref="NotificationChannel.Email"/> (SMTP) e, desde a
/// SPEC-0030, <see cref="NotificationChannel.WebPush"/> (VAPID); WhatsApp entra depois, implementando a mesma interface.
/// </summary>
internal interface INotificationChannel
{
    NotificationChannel Channel { get; }

    Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken);
}

/// <summary>SPEC-0018 — canais de entrega. SPEC-0030: Web Push (VAPID, ADR-0014).</summary>
internal enum NotificationChannel
{
    Email,
    WebPush,
}

/// <summary>SPEC-0018 — mensagem já renderizada, pronta para o canal.</summary>
internal abstract record Notification(string Recipient);

/// <summary>SPEC-0018 — e-mail renderizado pelo modelo: assunto, HTML (dados escapados) e texto puro.</summary>
internal sealed record EmailNotification(string Recipient, string Subject, string HtmlBody, string TextBody)
    : Notification(Recipient);

/// <summary>
/// SPEC-0030 — mensagem de Web Push para uma inscrição: o endpoint (no <see cref="Notification.Recipient"/>), as chaves
/// do navegador (<c>p256dh</c>, <c>auth</c>) e o conteúdo JSON <c>{title, body, url}</c>, cifrado pelo canal (aes128gcm).
/// </summary>
internal sealed record PushNotification(string Endpoint, string P256dh, string Auth, string Payload)
    : Notification(Endpoint);

/// <summary>
/// SPEC-0030 — falhas do canal Web Push (internas: não fazem parte do contrato público): a inscrição morreu (404/410,
/// removida), falha temporária (5xx, 429, rede: nova tentativa) ou recusa definitiva (outros 4xx).
/// </summary>
internal static class PushFailures
{
    public const string Gone = "notifications.push_gone";

    public const string Temporary = "notifications.push_temporary";

    public const string Rejected = "notifications.push_rejected";
}
