using Subsolo.Modules.Notifications.Domain.Notices;

namespace Subsolo.Modules.Notifications.Domain.Preferences;

/// <summary>
/// SPEC-0030 — o que o usuário quer receber (§2): show novo por push, resumo diário por e-mail e o lembrete na véspera
/// por push e por e-mail. E-mail é opt-in (ADR-0008).
/// </summary>
public sealed record NotificationPreferences(bool NewShowPush, bool DailyDigestEmail, bool ReminderPush, bool ReminderEmail)
{
    /// <summary>
    /// Padrões de quem ainda não ativou o push (§2): show novo desligado (liga ao ativar o push), resumo diário e lembrete
    /// por e-mail desligados (opt-in), lembrete por push ligado.
    /// </summary>
    public static NotificationPreferences Default { get; } = new(false, false, true, false);

    /// <summary>O aviso do tipo <paramref name="kind"/> pode sair por push.</summary>
    public bool AllowsPush(string kind) => kind switch
    {
        NoticeKinds.NewShow => NewShowPush,
        NoticeKinds.Reminder => ReminderPush,
        _ => false,
    };

    /// <summary>O aviso do tipo <paramref name="kind"/> pode sair por e-mail.</summary>
    public bool AllowsEmail(string kind) => kind switch
    {
        NoticeKinds.Digest => DailyDigestEmail,
        NoticeKinds.Reminder => ReminderEmail,
        _ => false,
    };
}
