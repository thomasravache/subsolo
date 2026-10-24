using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Notifications.Application.Features.Unsubscribe;
using Subsolo.Modules.Notifications.Domain.Notices;

namespace Subsolo.Modules.Notifications.Infrastructure.Unsubscribe;

/// <summary>
/// SPEC-0030 — o token de descadastro no formato de <see cref="UnsubscribeOptions"/>: <c>{payload}.{assinatura}</c>, com
/// o HMAC-SHA256 do texto de <c>payload</c> comparado em tempo constante. A chave vem de
/// <c>Notifications:Unsubscribe:SigningKey</c> (segredo, ADR-0008); sem ela, em Development usa uma chave aleatória do
/// processo (os links valem até reiniciar a API) e fora de Development não há token: os e-mails de aviso não saem e todo
/// link é inválido.
/// </summary>
internal sealed class UnsubscribeTokens : IUnsubscribeTokens
{
    /// <summary>Tamanho máximo aceito para um token (o real tem uns 120 caracteres).</summary>
    public const int MaxTokenLength = 512;

    private readonly byte[]? _key;

    public UnsubscribeTokens(IOptions<UnsubscribeOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        var configured = options.Value.SigningKey;
        _key = !string.IsNullOrEmpty(configured)
            ? Encoding.UTF8.GetBytes(configured)
            : environment.IsDevelopment() ? RandomNumberGenerator.GetBytes(32) : null;
    }

    public string? Create(Guid userId, string kind)
    {
        if (_key is null)
        {
            return null;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(new TokenPayload(userId.ToString("D"), kind));
        var payload = Base64Url.EncodeToString(json);
        return $"{payload}.{Base64Url.EncodeToString(Sign(_key, payload))}";
    }

    public UnsubscribeTarget? Read(string? token)
    {
        if (_key is null || token is not { Length: > 0 and <= MaxTokenLength })
        {
            return null;
        }

        var dot = token.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot != token.LastIndexOf('.') || dot == token.Length - 1)
        {
            return null;
        }

        var payload = token[..dot];
        if (!TryDecode(token[(dot + 1)..], out var signature)
            || !CryptographicOperations.FixedTimeEquals(signature, Sign(_key, payload))
            || !TryDecode(payload, out var json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TokenPayload>(json);
            return parsed is { U: { } user, K: { } kind }
                   && Guid.TryParse(user, out var userId)
                   && NoticeKinds.HasEmail(kind)
                ? new UnsubscribeTarget(userId, kind)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] Sign(byte[] key, string payload) => HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(payload));

    private static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (!Base64Url.IsValid(text))
        {
            return false;
        }

        bytes = Base64Url.DecodeFromChars(text);
        return true;
    }

    private sealed record TokenPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("u")] string? U,
        [property: System.Text.Json.Serialization.JsonPropertyName("k")] string? K);
}
