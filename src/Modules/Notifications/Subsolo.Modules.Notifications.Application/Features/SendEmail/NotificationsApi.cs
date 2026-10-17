using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application.Features.SendEmail;

/// <summary>
/// SPEC-0018 — implementação de <see cref="INotificationsApi"/>: valida destinatário e dados, renderiza o modelo e
/// entrega pelo canal de e-mail. Scaffold do contrato: ainda não implementado.
/// </summary>
internal sealed class NotificationsApi(IEnumerable<INotificationChannel> channels) : INotificationsApi
{
    private readonly IEnumerable<INotificationChannel> _channels = channels;

    public Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"SPEC-0018: INotificationsApi ainda não implementado ({_channels.Count()} canal(is)).");
}
