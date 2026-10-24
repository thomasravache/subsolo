using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Features.Subscriptions;

/// <summary>
/// SPEC-0030 — validação da inscrição de push (§5, contra SSRF): o endpoint é uma URL absoluta <c>https</c> de um
/// serviço de push conhecido (FCM, Mozilla, Apple) e as chaves <c>p256dh</c> e <c>auth</c> estão presentes; senão,
/// <c>notifications.invalid_subscription</c>. A comparação é pelo host exato (nada de usuário na URL, porta fora da
/// padrão ou endereço IP), então o canal só chama esses serviços. SPEC-0031 (H30-01): o WNS do Edge no Windows tem um
/// host por região (<c>wns2-bl2p.notify.windows.com</c>), aceito pelo sufixo exato <see cref="WnsHostSuffix"/> (com o
/// ponto: <c>evilnotify.windows.com</c> e <c>notify.windows.com.evil.example</c> continuam recusados).
/// </summary>
internal static class PushSubscriptionValidator
{
    /// <summary>Código do erro de inscrição inválida (400).</summary>
    public const string InvalidSubscription = "notifications.invalid_subscription";

    /// <summary>Tamanho máximo do endpoint (os serviços conhecidos ficam bem abaixo).</summary>
    public const int MaxEndpointLength = 2048;

    /// <summary>Tamanho máximo de cada chave em base64url (p256dh tem 87 caracteres; auth, 22).</summary>
    public const int MaxKeyLength = 128;

    /// <summary>Hosts dos serviços de push aceitos: FCM (Chrome, Edge, Android), Mozilla (Firefox) e Apple (Safari).</summary>
    public static readonly IReadOnlySet<string> KnownHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fcm.googleapis.com",
        "updates.push.services.mozilla.com",
        "web.push.apple.com",
    };

    /// <summary>SPEC-0031 (H30-01) — sufixo dos hosts do WNS (Windows Push Notification Services), com o ponto.</summary>
    public const string WnsHostSuffix = ".notify.windows.com";

    public static readonly Error Invalid = Error.Validation(
        InvalidSubscription,
        "A inscrição de push é inválida.",
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["endpoint"] = ["Use o endereço https de um serviço de push conhecido, com as chaves p256dh e auth."],
        });

    public static Result Validate(string? endpoint, string? p256dh, string? auth) =>
        IsKnownEndpoint(endpoint) && IsKey(p256dh) && IsKey(auth) ? Result.Success() : Result.Failure(Invalid);

    /// <summary>O endpoint é <c>https://&lt;serviço conhecido&gt;/…</c>, sem usuário, porta própria nem fragmento.</summary>
    public static bool IsKnownEndpoint(string? endpoint) =>
        endpoint is { Length: > 0 and <= MaxEndpointLength }
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Fragment)
        && uri.HostNameType == UriHostNameType.Dns
        && IsKnownHost(uri.Host);

    private static bool IsKnownHost(string host) =>
        KnownHosts.Contains(host)
        || (host.Length > WnsHostSuffix.Length && host.EndsWith(WnsHostSuffix, StringComparison.OrdinalIgnoreCase));

    private static bool IsKey(string? key) =>
        key is { Length: > 0 and <= MaxKeyLength } && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '=');
}
