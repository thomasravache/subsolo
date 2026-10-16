using System.Diagnostics;
using OpenTelemetry;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — processador de spans registrado antes dos exportadores: ao fim de cada span, mascara
/// e-mails e troca por <c>[REDACTED]</c> os atributos sensíveis (token, <c>Authorization</c>, <c>Cookie</c>...),
/// além de mascarar e-mails no nome e na descrição do status.
/// </summary>
internal sealed class RedactingActivityProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        List<KeyValuePair<string, object?>>? changes = null;
        foreach (var (key, value) in data.TagObjects)
        {
            if (PersonalDataRedactor.TryRedact(key, value, out var redacted))
            {
                (changes ??= []).Add(new KeyValuePair<string, object?>(key, redacted));
            }
        }

        if (changes is not null)
        {
            foreach (var (key, value) in changes)
            {
                data.SetTag(key, value);
            }
        }

        data.DisplayName = PersonalDataRedactor.MaskEmails(data.DisplayName);
        if (data.StatusDescription is { } description)
        {
            data.SetStatus(data.Status, PersonalDataRedactor.MaskEmails(description));
        }
    }
}
