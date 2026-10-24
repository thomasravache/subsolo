using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>Base64url sem preenchimento (RFC 4648 §5), como as chaves e o JWT do Web Push.</summary>
internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}

/// <summary>
/// Par de chaves VAPID (P-256) gerado no teste: a pública não comprimida (65 bytes) e a privada (escalar de 32 bytes),
/// em base64url, no formato de <c>Notifications:WebPush</c>. Nunca um segredo real.
/// </summary>
internal sealed record VapidKeys(string PublicKey, string PrivateKey)
{
    public static VapidKeys Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);
        return new VapidKeys(Base64Url.Encode(UncompressedPoint(parameters.Q)), Base64Url.Encode(parameters.D));
    }

    public static byte[] UncompressedPoint(ECPoint point) => [0x04, .. point.X!, .. point.Y!];

    public static ECParameters PublicParameters(byte[] uncompressed) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..65] },
    };
}

/// <summary>
/// Um navegador inscrito (o "user agent" do RFC 8291): chave ECDH P-256 própria e o segredo <c>auth</c> de 16 bytes,
/// expostos como <c>p256dh</c>/<c>auth</c> em base64url; decifra o corpo <c>aes128gcm</c> (RFC 8188) que o servidor
/// manda ao serviço de push, só com o BCL (ECDH, HKDF/HMAC, AES-GCM).
/// </summary>
internal sealed class PushSubscriber : IDisposable
{
    private const int TagSize = 16;
    private readonly ECDiffieHellman _key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _publicKey;
    private readonly byte[] _auth = RandomNumberGenerator.GetBytes(16);

    public PushSubscriber()
    {
        _publicKey = VapidKeys.UncompressedPoint(_key.ExportParameters(includePrivateParameters: false).Q);
    }

    public string P256dh => Base64Url.Encode(_publicKey);

    public string Auth => Base64Url.Encode(_auth);

    /// <summary>
    /// Decifra um corpo <c>aes128gcm</c> de um registro só: cabeçalho <c>salt(16) | rs(4) | idlen(1) | keyid(65 = chave
    /// pública efêmera do servidor)</c> e o texto cifrado com a tag; o texto claro termina no delimitador <c>0x02</c>
    /// seguido de zeros. Lança se a mensagem não foi cifrada para esta inscrição.
    /// </summary>
    public byte[] Decrypt(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var salt = body[..16];
        var recordSize = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4));
        var idLength = body[20];
        if (idLength != 65)
        {
            throw new InvalidDataException($"keyid com {idLength} bytes (esperado: a chave pública efêmera de 65 bytes)");
        }

        var serverPublic = body[21..86];
        var cipher = body[86..];
        if (cipher.Length > recordSize)
        {
            throw new InvalidDataException($"mais de um registro (rs = {recordSize}, cifrado = {cipher.Length})");
        }

        using var server = ECDiffieHellman.Create(VapidKeys.PublicParameters(serverPublic));
        var sharedSecret = _key.DeriveRawSecretAgreement(server.PublicKey);

        byte[] keyInfo = [.. "WebPush: info\0"u8, .. _publicKey, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, _auth, keyInfo);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        var plain = new byte[cipher.Length - TagSize];
        using (var aes = new AesGcm(cek, TagSize))
        {
            aes.Decrypt(nonce, cipher[..^TagSize], cipher[^TagSize..], plain);
        }

        var end = plain.Length - 1;
        while (end >= 0 && plain[end] == 0)
        {
            end--;
        }

        if (end < 0 || plain[end] != 0x02)
        {
            throw new InvalidDataException("sem o delimitador 0x02 do último registro");
        }

        return plain[..end];
    }

    public void Dispose() => _key.Dispose();
}

/// <summary>
/// Conferência do cabeçalho <c>Authorization: vapid t=&lt;JWT&gt;, k=&lt;chave pública&gt;</c> (RFC 8292): o JWT ES256
/// assinado pela chave privada VAPID, com <c>aud</c> = origem do endpoint, <c>exp</c> no futuro e até 24 h, e o
/// <c>sub</c> configurado.
/// </summary>
internal static class VapidAuthorization
{
    /// <summary>Lista do que está errado no cabeçalho (vazia = válido).</summary>
    public static IReadOnlyList<string> Problems(string? header, VapidKeys keys, Uri endpoint, string subject, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (header is null || !header.StartsWith("vapid ", StringComparison.Ordinal))
        {
            return [$"Authorization não é vapid: {header}"];
        }

        var parts = header["vapid ".Length..]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        var problems = new List<string>();
        if (!parts.TryGetValue("k", out var k) || k != keys.PublicKey)
        {
            problems.Add($"k diferente da chave pública VAPID: {k}");
        }

        if (!parts.TryGetValue("t", out var token) || token.Split('.') is not [var head, var payload, var signature])
        {
            problems.Add("t sem um JWT de três partes");
            return problems;
        }

        using (var verifier = ECDsa.Create(VapidKeys.PublicParameters(Base64Url.Decode(keys.PublicKey))))
        {
            if (!verifier.VerifyData(Encoding.ASCII.GetBytes($"{head}.{payload}"), Base64Url.Decode(signature), HashAlgorithmName.SHA256))
            {
                problems.Add("assinatura ES256 do JWT inválida para a chave pública VAPID");
            }
        }

        using var header0 = JsonDocument.Parse(Base64Url.Decode(head));
        if (header0.RootElement.GetProperty("alg").GetString() != "ES256")
        {
            problems.Add($"alg do JWT: {header0.RootElement}");
        }

        using var claims = JsonDocument.Parse(Base64Url.Decode(payload));
        var root = claims.RootElement;
        var audience = endpoint.GetLeftPart(UriPartial.Authority);
        if (!root.TryGetProperty("aud", out var aud) || aud.GetString() != audience)
        {
            problems.Add($"aud diferente de {audience}: {root}");
        }

        if (!root.TryGetProperty("sub", out var sub) || sub.GetString() != subject)
        {
            problems.Add($"sub diferente de {subject}: {root}");
        }

        if (!root.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number)
        {
            problems.Add($"sem exp: {root}");
        }
        else
        {
            var expires = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (expires <= now || expires > now.AddHours(24).AddMinutes(1))
            {
                problems.Add($"exp fora de (agora, agora + 24 h]: {expires:O}");
            }
        }

        return problems;
    }
}
