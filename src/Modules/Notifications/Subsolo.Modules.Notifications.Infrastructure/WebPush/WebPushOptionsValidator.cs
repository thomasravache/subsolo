using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Subsolo.Modules.Notifications.Infrastructure.WebPush;

/// <summary>
/// SPEC-0030 — confere <see cref="WebPushOptions"/> na inicialização: sem chaves, o canal fica desligado (ver
/// <see cref="WebPushKeys"/>); com elas, as duas precisam estar presentes e formar um par P-256 válido em base64url, e o
/// <c>Subject</c> precisa ser <c>mailto:</c> ou <c>https:</c> (RFC 8292).
/// </summary>
internal sealed class WebPushOptionsValidator : IValidateOptions<WebPushOptions>
{
    public ValidateOptionsResult Validate(string? name, WebPushOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hasPublic = !string.IsNullOrEmpty(options.PublicKey);
        var hasPrivate = !string.IsNullOrEmpty(options.PrivateKey);
        if (!hasPublic && !hasPrivate)
        {
            return ValidateOptionsResult.Success;
        }

        if (hasPublic != hasPrivate)
        {
            return ValidateOptionsResult.Fail(
                $"{WebPushOptions.SectionName}: configure PublicKey e PrivateKey juntas (par VAPID P-256 em base64url).");
        }

        if (!IsKeyPair(options.PublicKey, options.PrivateKey))
        {
            return ValidateOptionsResult.Fail(
                $"{WebPushOptions.SectionName}: PublicKey (65 bytes) e PrivateKey (32 bytes) não formam um par P-256 em base64url.");
        }

        return options.Subject.StartsWith("mailto:", StringComparison.Ordinal)
               || options.Subject.StartsWith("https://", StringComparison.Ordinal)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{WebPushOptions.SectionName}:Subject precisa ser mailto: ou https: (RFC 8292).");
    }

    private static bool IsKeyPair(string publicKey, string privateKey)
    {
        if (!Base64Url.IsValid(publicKey) || !Base64Url.IsValid(privateKey))
        {
            return false;
        }

        var q = Base64Url.DecodeFromChars(publicKey);
        var d = Base64Url.DecodeFromChars(privateKey);
        if (q.Length != 65 || q[0] != 0x04 || d.Length != 32)
        {
            return false;
        }

        try
        {
            using var key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = q[1..33], Y = q[33..] },
                D = d,
            });
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
