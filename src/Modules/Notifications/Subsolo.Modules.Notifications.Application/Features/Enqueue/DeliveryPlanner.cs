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
    /// <summary>Esqueleto (G1): implementado no G2.</summary>
    public static IReadOnlyList<PlannedDelivery> Plan(
        Notice notice,
        IReadOnlyCollection<RecipientChannels> recipients,
        IReadOnlySet<PlannedDelivery> alreadyDelivered) =>
        throw new NotImplementedException("SPEC-0030: DeliveryPlanner ainda não implementado.");
}

/// <summary>SPEC-0030 — o que o planejador sabe de um destinatário: preferências e canais disponíveis.</summary>
internal sealed record RecipientChannels(
    Guid UserId,
    NotificationPreferences Preferences,
    bool HasPushSubscription,
    bool HasEmail);

/// <summary>SPEC-0030 — uma entrega planejada (ou já registrada): usuário e canal.</summary>
internal sealed record PlannedDelivery(Guid UserId, NotificationChannel Channel);
