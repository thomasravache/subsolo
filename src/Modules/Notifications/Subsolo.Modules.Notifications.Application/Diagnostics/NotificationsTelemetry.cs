using System.Diagnostics;
using System.Diagnostics.Metrics;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application.Diagnostics;

/// <summary>
/// SPEC-0018 — sinais do módulo Notifications (Plano de Rollout): o span <c>notifications.email.send</c> e o contador
/// <c>subsolo.notifications.emails</c>, os dois com o modelo e o resultado. Sem dados pessoais nem conteúdo nas tags:
/// o destinatário e o link (uma credencial) nunca entram nos sinais (ADR-0008).
/// </summary>
public static class NotificationsTelemetry
{
    /// <summary>Nome da <see cref="ActivitySource"/> (coberta pelo <c>Subsolo.*</c> do ServiceDefaults).</summary>
    public const string SourceName = "Subsolo.Notifications";

    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Notifications";

    /// <summary>Nome do span de cada envio de e-mail.</summary>
    public const string SendEmailActivity = "notifications.email.send";

    /// <summary>Tag com o modelo do e-mail (<c>MagicLink</c>).</summary>
    public const string TemplateTag = "subsolo.notifications.template";

    /// <summary>Tag com o resultado: <see cref="Sent"/> ou o código de erro de <see cref="NotificationsErrors"/>.</summary>
    public const string ResultTag = "subsolo.notifications.result";

    /// <summary>Resultado de um envio aceito pelo servidor SMTP.</summary>
    public const string Sent = "sent";

    internal static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Emails = Meter.CreateCounter<long>(
        "subsolo.notifications.emails", unit: "{email}", description: "E-mails transacionais, por modelo e resultado.");

    /// <summary>Abre o span do envio (ou <see langword="null"/> sem ouvinte), já com o modelo.</summary>
    internal static Activity? StartSend(EmailTemplate template) =>
        Source.StartActivity(SendEmailActivity)?.SetTag(TemplateTag, template.ToString());

    /// <summary>Fecha o resultado no span e conta o envio.</summary>
    internal static void Completed(Activity? activity, EmailTemplate template, string result)
    {
        if (activity is not null)
        {
            activity.SetTag(ResultTag, result);
            activity.SetStatus(result == Sent ? ActivityStatusCode.Ok : ActivityStatusCode.Error, result == Sent ? null : result);
        }

        Emails.Add(1, new TagList { { TemplateTag, template.ToString() }, { ResultTag, result } });
    }
}
