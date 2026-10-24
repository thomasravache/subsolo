using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Features.SendEmail;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SPEC-0018 — canal de e-mail por SMTP (MailKit, ADR-0014): uma conexão por envio, com a segurança de
/// <see cref="SmtpOptions"/> e timeout de 10 s para tudo (conexão, autenticação e envio). SMTP fora do ar, lento ou que
/// recusa a mensagem vira <see cref="NotificationsErrors.EmailUnavailable"/>, com o destinatário só mascarado no log e
/// sem o conteúdo (o link é uma credencial, ADR-0008). Sem retry: o usuário pede de novo.
/// </summary>
internal sealed partial class SmtpEmailChannel(IOptions<SmtpOptions> options, ILogger<SmtpEmailChannel> logger)
    : INotificationChannel
{
    /// <summary>Prazo de cada envio, do início da conexão ao fim da transação SMTP.</summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private static readonly Error Unavailable = Error.Failure(
        NotificationsErrors.EmailUnavailable, "O serviço de e-mail está indisponível. Tente de novo em instantes.");

    private readonly SmtpOptions _options = options.Value;

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification is not EmailNotification email)
        {
            throw new ArgumentException($"O canal de e-mail não envia {notification.GetType().Name}.", nameof(notification));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        try
        {
            using var message = Message(email);
            using var client = new SmtpClient { Timeout = (int)SendTimeout.TotalMilliseconds };
            await client.ConnectAsync(_options.Host, _options.Port, SocketOptions(_options.Security), timeout.Token);
            if (!string.IsNullOrEmpty(_options.User))
            {
                await client.AuthenticateAsync(_options.User, _options.Password ?? string.Empty, timeout.Token);
            }

            await client.SendAsync(message, timeout.Token);
            await DisconnectAsync(client, timeout.Token);
            return Result.Success();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, exception, EmailRecipient.Mask(email.Recipient));
            return Result.Failure(Unavailable);
        }
    }

    /// <summary>
    /// QUIT depois do envio aceito: só cortesia com o servidor. Se falhar, a mensagem já foi entregue ao SMTP, então o
    /// resultado continua sucesso (o <c>Dispose</c> do cliente fecha a conexão), mesmo com timeout ou cancelamento
    /// nesse ponto, e a falha vai só para o log de debug.
    /// </summary>
    private async Task DisconnectAsync(SmtpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception exception)
        {
            LogDisconnectFailed(logger, exception);
        }
    }

    private MimeMessage Message(EmailNotification email)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.Recipient));
        message.Subject = email.Subject;
        foreach (var (name, value) in email.Headers)
        {
            message.Headers.Add(name, value);
        }

        message.Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = email.TextBody }.ToMessageBody();
        return message;
    }

    private static SecureSocketOptions SocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };

    [LoggerMessage(
        EventName = "EmailUnavailable",
        Level = LogLevel.Warning,
        Message = "E-mail para {Recipient} não enviado: SMTP indisponível, lento ou recusou a mensagem")]
    private static partial void LogUnavailable(ILogger logger, Exception exception, string recipient);

    [LoggerMessage(
        EventName = "EmailDisconnectFailed",
        Level = LogLevel.Debug,
        Message = "SMTP: o QUIT depois de um envio aceito falhou; a conexão é fechada sem ele")]
    private static partial void LogDisconnectFailed(ILogger logger, Exception exception);
}
