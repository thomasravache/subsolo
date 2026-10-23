using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Domain.Preferences;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application.Features.Enqueue;

/// <summary>
/// SPEC-0030 — planejador de entregas de <c>EnqueueAsync</c> (regra pura, UT-02): para cada destinatário, uma entrega
/// por canal permitido pelo tipo do aviso, pelas preferências e pelos canais disponíveis (push só com inscrição, e-mail
/// só com endereço), sem repetir as entregas já registradas para a mesma chave.
/// </summary>
internal static class DeliveryPlanner
{
    public static IReadOnlyList<PlannedDelivery> Plan(
        Notice notice,
        IReadOnlyCollection<RecipientChannels> recipients,
        IReadOnlySet<PlannedDelivery> alreadyDelivered)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(alreadyDelivered);

        var planned = new List<PlannedDelivery>();
        var seen = new HashSet<PlannedDelivery>();
        foreach (var recipient in recipients)
        {
            if (recipient.HasPushSubscription && recipient.Preferences.AllowsPush(notice.Kind))
            {
                Add(new PlannedDelivery(recipient.UserId, NotificationChannel.WebPush));
            }

            if (recipient.HasEmail && recipient.Preferences.AllowsEmail(notice.Kind))
            {
                Add(new PlannedDelivery(recipient.UserId, NotificationChannel.Email));
            }
        }

        return planned;

        void Add(PlannedDelivery delivery)
        {
            if (!alreadyDelivered.Contains(delivery) && seen.Add(delivery))
            {
                planned.Add(delivery);
            }
        }
    }
}

/// <summary>SPEC-0030 — o que o planejador sabe de um destinatário: preferências e canais disponíveis.</summary>
internal sealed record RecipientChannels(
    Guid UserId,
    NotificationPreferences Preferences,
    bool HasPushSubscription,
    bool HasEmail);

/// <summary>SPEC-0030 — uma entrega planejada (ou já registrada): usuário e canal.</summary>
internal sealed record PlannedDelivery(Guid UserId, NotificationChannel Channel);
