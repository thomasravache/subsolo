using System.Collections.Concurrent;
using Lib.Net.Http.WebPush.Authentication;

namespace Subsolo.Modules.Notifications.Infrastructure.WebPush;

/// <summary>
/// SPEC-0030 — cache dos JWT VAPID por origem do serviço de push (um por FCM, Mozilla e Apple): evita assinar um token por
/// mensagem. Um token só sai do cache com mais de <see cref="MinimumRemaining"/> de validade.
/// </summary>
internal sealed class VapidTokenCache(TimeProvider clock) : IVapidTokenCache
{
    public static readonly TimeSpan MinimumRemaining = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expiration)> _tokens = new(StringComparer.Ordinal);

    public string? Get(string audience) =>
        _tokens.TryGetValue(audience, out var cached) && cached.Expiration - clock.GetUtcNow() > MinimumRemaining
            ? cached.Token
            : null;

    public void Put(string audience, DateTimeOffset expiration, string token) => _tokens[audience] = (token, expiration);
}
