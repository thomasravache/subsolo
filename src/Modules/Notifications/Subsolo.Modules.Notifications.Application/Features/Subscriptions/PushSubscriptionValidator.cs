using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Features.Subscriptions;

/// <summary>
/// SPEC-0030 — validação da inscrição de push (§5, contra SSRF): o endpoint é uma URL absoluta <c>https</c> de um
/// serviço de push conhecido (FCM, Mozilla, Apple) e as chaves <c>p256dh</c> e <c>auth</c> estão presentes; senão,
/// <c>notifications.invalid_subscription</c>.
/// </summary>
internal static class PushSubscriptionValidator
{
    /// <summary>Código do erro de inscrição inválida (400).</summary>
    public const string InvalidSubscription = "notifications.invalid_subscription";

    /// <summary>Esqueleto (G1): implementado no G2.</summary>
    public static Result Validate(string? endpoint, string? p256dh, string? auth) =>
        throw new NotImplementedException("SPEC-0030: PushSubscriptionValidator ainda não implementado.");
}
