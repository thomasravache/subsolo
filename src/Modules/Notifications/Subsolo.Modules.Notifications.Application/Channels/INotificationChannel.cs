using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Channels;

/// <summary>
/// SPEC-0018 (ADR-0014) — canal plugável de entrega. Nesta spec só existe o <see cref="NotificationChannel.Email"/>
/// (SMTP); push e WhatsApp entram depois, implementando a mesma interface.
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
