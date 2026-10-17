namespace Subsolo.Modules.Notifications.IntegrationEvents;

/// <summary>SPEC-0018 — códigos de erro de <see cref="INotificationsApi"/> (contrato consumido pela SPEC-0016).</summary>
public static class NotificationsErrors
{
    /// <summary><c>To</c> fora do formato de e-mail ou com mais de 254 caracteres.</summary>
    public const string InvalidRecipient = "notifications.invalid_recipient";

    /// <summary>Chave obrigatória ausente ou valor inválido para o modelo.</summary>
    public const string InvalidData = "notifications.invalid_data";

    /// <summary>SMTP fora do ar, recusou a mensagem ou passou de 10 s.</summary>
    public const string EmailUnavailable = "notifications.email_unavailable";
}
