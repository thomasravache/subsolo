using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Domain.MagicLinks;

/// <summary>
/// SPEC-0016 — link mágico de entrada (ADR-0003), tabela <c>users.magic_link_tokens</c>: token aleatório de 256 bits
/// (só na URL do e-mail) guardado só como hash (<see cref="TokenHash"/>), e-mail normalizado, <see cref="ReturnTo"/>
/// já saneado, válido por <see cref="Lifetime"/> e de uso único (<see cref="UsedAtUtc"/>).
/// </summary>
public sealed class MagicLinkToken : Entity
{
    /// <summary>Validade do link desde a emissão.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Tamanho do token em bytes (256 bits).</summary>
    public const int TokenBytes = 32;

    /// <summary>Tamanho do <see cref="TokenHash"/>: SHA-256 em hexadecimal minúsculo.</summary>
    public const int TokenHashLength = 64;

    /// <summary>Tamanho máximo do <see cref="ReturnTo"/> gravado (um caminho mais longo vira <c>/</c>).</summary>
    public const int ReturnToMaxLength = 2048;

    private MagicLinkToken()
    {
    }

    /// <summary>Hash do token (o token em claro só existe no link do e-mail).</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>E-mail normalizado de quem pediu o link.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Caminho relativo do próprio site para onde a entrada volta (já saneado; padrão <c>/</c>).</summary>
    public string ReturnTo { get; private set; } = "/";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary><see cref="CreatedAtUtc"/> + <see cref="Lifetime"/>.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Quando o link foi consumido; <see langword="null"/> enquanto não usado.</summary>
    public DateTimeOffset? UsedAtUtc { get; private set; }

    /// <summary>
    /// Emite um link novo para <paramref name="normalizedEmail"/>: devolve a entidade (só com o hash) e o token em claro
    /// para a URL do e-mail.
    /// </summary>
    public static (MagicLinkToken Token, string Value) Issue(string normalizedEmail, string returnTo, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrEmpty(returnTo);
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var token = new MagicLinkToken
        {
            TokenHash = HashToken(value),
            Email = normalizedEmail,
            ReturnTo = returnTo,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + Lifetime,
        };
        return (token, value);
    }

    /// <summary>O hash SHA-256 (hexadecimal minúsculo) do texto do token, a chave do link no banco.</summary>
    public static string HashToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>O link ainda serve em <paramref name="nowUtc"/>: não usado e dentro da validade.</summary>
    public bool IsUsable(DateTimeOffset nowUtc) => UsedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>Marca o link como usado em <paramref name="nowUtc"/>; usar de novo não muda a data.</summary>
    public void MarkUsed(DateTimeOffset nowUtc) => UsedAtUtc ??= nowUtc;
}
