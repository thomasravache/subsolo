using System.Collections;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 — estado de log já mascarado: os pares chave/valor (atributos do log exportado) e a mensagem formatada.
/// </summary>
internal sealed class RedactedLogState(KeyValuePair<string, object?>[] values, string message)
    : IReadOnlyList<KeyValuePair<string, object?>>
{
    public KeyValuePair<string, object?> this[int index] => values[index];

    public int Count => values.Length;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, object?>>)values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => message;

    /// <summary>
    /// Versão mascarada de <paramref name="values"/>, ou <see langword="null"/> quando nenhum valor muda (a mensagem
    /// só traz o modelo e os valores, então também não muda). A mensagem (<paramref name="format"/>, chamada só quando
    /// algo muda) recebe os mesmos valores trocados e o mascaramento de e-mails.
    /// </summary>
    public static RedactedLogState? From(IEnumerable<KeyValuePair<string, object?>> pairs, Func<string?> format)
    {
        var values = pairs as IReadOnlyList<KeyValuePair<string, object?>> ?? [.. pairs];
        KeyValuePair<string, object?>[]? redacted = null;
        for (var i = 0; i < values.Count; i++)
        {
            var (key, value) = values[i];
            if (PersonalDataRedactor.TryRedact(key, value, out var replacement))
            {
                redacted ??= [.. values];
                redacted[i] = new KeyValuePair<string, object?>(key, replacement);
            }
        }

        if (redacted is null)
        {
            return null;
        }

        var text = format() ?? string.Empty;
        for (var i = 0; i < values.Count; i++)
        {
            if (!ReferenceEquals(values[i].Value, redacted[i].Value) && values[i].Value?.ToString() is { Length: > 0 } original)
            {
                text = text.Replace(original, redacted[i].Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
            }
        }

        return new RedactedLogState(redacted, PersonalDataRedactor.MaskEmails(text));
    }
}
