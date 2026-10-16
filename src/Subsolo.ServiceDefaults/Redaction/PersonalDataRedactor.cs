using System.Text.RegularExpressions;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — mascaramento de dados pessoais e segredos nos sinais de observabilidade: e-mail →
/// <c>j***@d***.com</c> em qualquer texto; valor de chave sensível (token, <c>Authorization</c>, <c>Cookie</c>,
/// senha, segredo, chave de API) → <see cref="Redacted"/>.
/// </summary>
internal static partial class PersonalDataRedactor
{
    public const string Redacted = "[REDACTED]";

    /// <summary>Trechos de nome de chave (sem diferenciar maiúsculas) cujo valor nunca sai em texto puro.</summary>
    private static readonly string[] SensitiveKeyParts =
        ["authorization", "cookie", "token", "password", "passwd", "secret", "apikey", "api_key", "api-key"];

    /// <summary>A chave <paramref name="key"/> carrega um segredo (o valor inteiro é trocado por <see cref="Redacted"/>).</summary>
    public static bool IsSensitiveKey(string key) =>
        SensitiveKeyParts.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Valor mascarado para a chave <paramref name="key"/>; devolve <see langword="false"/> (e o próprio valor) quando
    /// nada precisa mudar.
    /// </summary>
    public static bool TryRedact(string key, object? value, out object? redacted)
    {
        redacted = value;
        if (value is null)
        {
            return false;
        }

        if (IsSensitiveKey(key))
        {
            redacted = Redacted;
            return true;
        }

        if (value is string text && TryMaskEmails(text, out var masked))
        {
            redacted = masked;
            return true;
        }

        return false;
    }

    /// <summary>Texto com cada e-mail (também com <c>@</c> codificado como <c>%40</c>) mascarado.</summary>
    public static string MaskEmails(string text) => TryMaskEmails(text, out var masked) ? masked : text;

    private static bool TryMaskEmails(string text, out string masked)
    {
        if (!text.Contains('@', StringComparison.Ordinal) && !text.Contains("%40", StringComparison.OrdinalIgnoreCase))
        {
            masked = text;
            return false;
        }

        masked = Email().Replace(text, match =>
            $"{match.Groups["local"].Value[0]}***@{match.Groups["domain"].Value[0]}***.{match.Groups["tld"].Value}");
        return !ReferenceEquals(masked, text) && !string.Equals(masked, text, StringComparison.Ordinal);
    }

    [GeneratedRegex(
        @"(?<local>[A-Za-z0-9._%+-]+)(?:@|%40)(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*)\.(?<tld>[A-Za-z]{2,})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex Email();
}
