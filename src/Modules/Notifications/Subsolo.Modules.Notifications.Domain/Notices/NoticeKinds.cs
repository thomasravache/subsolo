namespace Subsolo.Modules.Notifications.Domain.Notices;

/// <summary>
/// SPEC-0030 — os tipos de aviso (§6): show novo de quem o fã segue (só push), lembrete na véspera (push e/ou e-mail) e
/// resumo diário (só e-mail).
/// </summary>
public static class NoticeKinds
{
    public const string NewShow = "new_show";

    public const string Reminder = "reminder";

    public const string Digest = "digest";

    /// <summary><paramref name="kind"/> é um dos tipos conhecidos.</summary>
    public static bool IsKnown(string? kind) => kind is NewShow or Reminder or Digest;

    /// <summary>Os tipos que também saem por e-mail (e têm link de descadastro): <c>digest</c> e <c>reminder</c>.</summary>
    public static bool HasEmail(string? kind) => kind is Digest or Reminder;
}

/// <summary>SPEC-0030 — os canais gravados em <c>notifications.deliveries.channel</c> (Emenda de 2026-10-23).</summary>
public static class DeliveryChannels
{
    public const string Push = "push";

    public const string Email = "email";
}

/// <summary>SPEC-0030 — situação de uma entrega: na fila (com novas tentativas), entregue ou desistida.</summary>
public static class DeliveryStatuses
{
    public const string Pending = "pending";

    public const string Sent = "sent";

    public const string Failed = "failed";
}
