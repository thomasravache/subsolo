namespace Subsolo.Modules.Notifications.Application.Features.Unsubscribe;

/// <summary>
/// SPEC-0031 (H30-04, RFC 8058) — o descadastro em um clique dos e-mails de aviso: os cabeçalhos
/// <c>List-Unsubscribe: &lt;{App:PublicUrl}/api/v1/notifications/unsubscribe/one-click?token=…&gt;</c> e
/// <c>List-Unsubscribe-Post: List-Unsubscribe=One-Click</c>; o provedor de e-mail faz o <c>POST</c> com esse corpo
/// (<c>application/x-www-form-urlencoded</c>), e o token assinado é a autorização.
/// </summary>
internal static class OneClickUnsubscribe
{
    public const string ListUnsubscribeHeader = "List-Unsubscribe";

    public const string ListUnsubscribePostHeader = "List-Unsubscribe-Post";

    /// <summary>O valor do <c>List-Unsubscribe-Post</c> e o corpo do <c>POST</c> do provedor.</summary>
    public const string ListUnsubscribePostValue = "List-Unsubscribe=One-Click";
}
