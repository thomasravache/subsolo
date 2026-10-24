namespace Subsolo.Modules.Notifications.Application.Features.Enqueue;

/// <summary>
/// SPEC-0030 — a chave pública VAPID que o navegador usa no <c>pushManager.subscribe</c> (<c>GET /api/v1/push/public-key</c>);
/// <see langword="null"/> quando o canal Web Push não está configurado.
/// </summary>
internal interface IWebPushKeys
{
    string? PublicKey { get; }
}
