using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;

namespace Subsolo.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SPEC-0018 — canal de e-mail por SMTP (MailKit), com timeout de 10 s por envio. Scaffold do contrato: ainda não
/// implementado.
/// </summary>
internal sealed class SmtpEmailChannel : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0018: SmtpEmailChannel ainda não implementado.");
}
