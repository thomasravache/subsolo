using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;

namespace Subsolo.Modules.Notifications.Infrastructure.WebPush;

/// <summary>
/// SPEC-0030 — canal Web Push (ADR-0014): VAPID direto (esquema <c>vapid t=…, k=…</c>, JWT ES256 com validade de 12 h) e
/// o conteúdo cifrado em <c>aes128gcm</c> (RFC 8291), pelo Lib.Net.Http.WebPush, com toda chamada saindo do
/// <c>HttpClient</c> <see cref="WebPushOptions.HttpClientName"/>. O resultado diz ao envio o que fazer: 404/410 →
/// <see cref="PushFailures.Gone"/> (remover a inscrição), 429/5xx/rede/tempo → <see cref="PushFailures.Temporary"/>
/// (nova tentativa), outro 4xx → <see cref="PushFailures.Rejected"/>. O log leva só o serviço de push, nunca o endpoint.
/// </summary>
internal sealed partial class WebPushChannel(
    IHttpClientFactory httpClients,
    WebPushKeys keys,
    VapidTokenCache tokens,
    ILogger<WebPushChannel> logger) : INotificationChannel
{
    /// <summary>Quanto tempo o serviço de push guarda a mensagem se o aparelho estiver desligado (1 dia).</summary>
    public const int TimeToLiveSeconds = 24 * 60 * 60;

    /// <summary>Validade do JWT VAPID (12 h; o RFC 8292 permite até 24 h).</summary>
    public const int TokenLifetimeSeconds = 12 * 60 * 60;

    /// <summary>Prazo de cada chamada ao serviço de push.</summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    public NotificationChannel Channel => NotificationChannel.WebPush;

    public async Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification is not PushNotification push)
        {
            throw new ArgumentException($"O canal Web Push não envia {notification.GetType().Name}.", nameof(notification));
        }

        if (keys.PublicKey is null || keys.PrivateKey is null)
        {
            LogNotConfigured(logger);
            return Failure(PushFailures.Rejected, "Canal Web Push sem chaves VAPID.");
        }

        var service = new Uri(push.Endpoint).Host;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        using var vapid = new VapidAuthentication(keys.PublicKey, keys.PrivateKey)
        {
            Subject = keys.Subject,
            Expiration = TokenLifetimeSeconds,
            TokenCache = tokens,
        };
        var client = new PushServiceClient(httpClients.CreateClient(WebPushOptions.HttpClientName))
        {
            DefaultAuthentication = vapid,
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
            DefaultTimeToLive = TimeToLiveSeconds,
            AutoRetryAfter = false,
        };
        var subscription = new PushSubscription { Endpoint = push.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, push.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, push.Auth);

        try
        {
            await client.RequestPushMessageDeliveryAsync(
                subscription, new PushMessage(push.Payload) { Urgency = PushMessageUrgency.Normal }, timeout.Token);
            return Result.Success();
        }
        catch (PushServiceClientException exception)
        {
            var code = exception.StatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.Gone => PushFailures.Gone,
                HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout => PushFailures.Temporary,
                >= HttpStatusCode.InternalServerError => PushFailures.Temporary,
                _ => PushFailures.Rejected,
            };
            LogRefused(logger, service, (int)exception.StatusCode, code);
            return Failure(code, $"O serviço de push respondeu {(int)exception.StatusCode}.");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                          && exception is HttpRequestException or OperationCanceledException)
        {
            LogUnreachable(logger, exception, service);
            return Failure(PushFailures.Temporary, "O serviço de push não respondeu.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Security.Cryptography.CryptographicException)
        {
            // Chaves da inscrição que não servem para cifrar: a inscrição não tem como receber nada.
            LogInvalidKeys(logger, exception, service);
            return Failure(PushFailures.Gone, "As chaves da inscrição são inválidas.");
        }
    }

    private static Result Failure(string code, string description) => Result.Failure(Error.Failure(code, description));

    [LoggerMessage(EventName = "WebPushNotConfigured", Level = LogLevel.Error, Message = "Web Push sem chaves VAPID (Notifications:WebPush): push não enviado")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(EventName = "WebPushRefused", Level = LogLevel.Information, Message = "Push para {PushService} recusado com {StatusCode} ({Result})")]
    private static partial void LogRefused(ILogger logger, string pushService, int statusCode, string result);

    [LoggerMessage(EventName = "WebPushUnreachable", Level = LogLevel.Warning, Message = "Push para {PushService} sem resposta")]
    private static partial void LogUnreachable(ILogger logger, Exception exception, string pushService);

    [LoggerMessage(EventName = "WebPushInvalidKeys", Level = LogLevel.Warning, Message = "Push para {PushService} não cifrado: chaves da inscrição inválidas")]
    private static partial void LogInvalidKeys(ILogger logger, Exception exception, string pushService);
}
