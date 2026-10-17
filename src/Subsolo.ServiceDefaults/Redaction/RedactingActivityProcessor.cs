using System.Diagnostics;
using OpenTelemetry;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — processador de spans registrado antes dos exportadores: ao fim de cada span, mascara
/// e-mails (também no <c>ToString()</c> de valores que não são texto) e troca por <c>[REDACTED]</c> os atributos
/// sensíveis (token, <c>Authorization</c>, <c>Cookie</c>...), além de mascarar e-mails no nome e na descrição do
/// status. Os eventos <c>exception</c> (<see cref="Activity.AddException"/>) já nascem mascarados: um
/// <see cref="ActivityListener"/> com <see cref="ActivityListener.ExceptionRecorder"/> grava
/// <c>exception.message</c> e <c>exception.stacktrace</c> mascarados antes de o <see cref="Activity"/> gravar os
/// originais (ele só preenche as tags que ainda não existem).
/// </summary>
internal sealed class RedactingActivityProcessor : BaseProcessor<Activity>
{
    private const string ExceptionMessageTag = "exception.message";
    private const string ExceptionStackTraceTag = "exception.stacktrace";
    private const string ExceptionTypeTag = "exception.type";

    private readonly ActivityListener _exceptions;

    public RedactingActivityProcessor()
    {
        _exceptions = new ActivityListener
        {
            ShouldListenTo = static _ => true,
            ExceptionRecorder = RecordMaskedException,
        };
        ActivitySource.AddActivityListener(_exceptions);
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _exceptions.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void RecordMaskedException(Activity activity, Exception exception, ref TagList tags)
    {
        bool hasMessage = false, hasStackTrace = false, hasType = false;
        foreach (var (key, _) in tags)
        {
            hasMessage |= key == ExceptionMessageTag;
            hasStackTrace |= key == ExceptionStackTraceTag;
            hasType |= key == ExceptionTypeTag;
        }

        if (!hasMessage)
        {
            tags.Add(ExceptionMessageTag, PersonalDataRedactor.MaskEmails(exception.Message));
        }

        if (!hasStackTrace)
        {
            tags.Add(ExceptionStackTraceTag, PersonalDataRedactor.MaskEmails(exception.ToString()));
        }

        if (!hasType)
        {
            tags.Add(ExceptionTypeTag, exception.GetType().ToString());
        }
    }
}
