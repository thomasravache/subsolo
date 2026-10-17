using System.Text.RegularExpressions;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — mascaramento de dados pessoais e segredos nos sinais de observabilidade: e-mail →
/// <c>j***@d***.com</c> em qualquer texto (também no <c>ToString()</c> de valores que não são texto); valor de chave
/// sensível (token, <c>Authorization</c>, <c>Cookie</c>, senha, segredo, chave de API) → <see cref="Redacted"/>.
/// Falha fechado: se o mascaramento não terminar (timeout da regex), o texto inteiro sai <see cref="Redacted"/>.
/// </summary>
internal static partial class PersonalDataRedactor
{
    public const string Redacted = "[REDACTED]";

    /// <summary>Trechos de nome de chave (sem diferenciar maiúsculas) cujo valor nunca sai em texto puro.</summary>
    private static readonly string[] SensitiveKeyParts =
        ["authorization", "cookie", "token", "password", "passwd", "secret", "apikey", "api_key", "api-key"];

    /// <summary>A chave <paramref name="key"/> carrega um segredo (o valor inteiro é trocado por <see cref="Redacted"/>).</summary>
    public static bool IsSensitiveKey(string key)
    {
        foreach (var part in SensitiveKeyParts)
        {
            if (key.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Valor mascarado para a chave <paramref name="key"/>; devolve <see langword="false"/> (e o próprio valor) quando
    /// nada precisa mudar. Um valor que não é texto e cujo <c>ToString()</c> traz um e-mail vira o texto mascarado.
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

        var text = value switch
        {
            string s => s,
            _ when IsPlainValue(value) => null,
            _ => value.ToString(),
        };
        if (text is not null && TryMaskEmails(text, out var masked))
        {
            redacted = masked;
            return true;
        }

        return false;
    }

    /// <summary>Texto com cada e-mail (também com <c>@</c> codificado como <c>%40</c>) mascarado.</summary>
    public static string MaskEmails(string text) => TryMaskEmails(text, out var masked) ? masked : text;

    /// <summary>
    /// Mascara os e-mails de <paramref name="text"/>; devolve <see langword="false"/> quando nada muda. Se a regex
    /// estourar o tempo, <paramref name="masked"/> é <see cref="Redacted"/> (falha fechado, sem lançar).
    /// </summary>
    public static bool TryMaskEmails(string text, out string masked)
    {
        if (!text.Contains('@', StringComparison.Ordinal) && !text.Contains("%40", StringComparison.OrdinalIgnoreCase))
        {
            masked = text;
            return false;
        }

        try
        {
            masked = Email().Replace(text, match =>
                $"{match.Groups["local"].Value[0]}***@{match.Groups["domain"].Value[0]}***.{match.Groups["tld"].Value}");
        }
        catch (RegexMatchTimeoutException)
        {
            masked = Redacted;
            return true;
        }

        return !string.Equals(masked, text, StringComparison.Ordinal);
    }

    /// <summary>Valores cujo texto nunca traz um e-mail: números, datas, Guid, enum, bool.</summary>
    private static bool IsPlainValue(object value) =>
        value is bool or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double
            or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum;

    [GeneratedRegex(
        @"(?<local>[A-Za-z0-9._%+-]+)(?:@|%40)(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*)\.(?<tld>[A-Za-z]{2,})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex Email();
}
