namespace Subsolo.Modules.Notifications.Infrastructure.Unsubscribe;

/// <summary>
/// SPEC-0030 — chave do token de descadastro (seção <see cref="SectionName"/>, segredo, ADR-0008). Formato do token
/// (usado pelos testes): <c>{payload}.{assinatura}</c>, em que <c>payload</c> é o base64url (sem preenchimento) do JSON
/// UTF-8 <c>{"u":"&lt;id do usuário&gt;","k":"digest"|"reminder"}</c> e <c>assinatura</c> é o base64url (sem
/// preenchimento) do HMAC-SHA256 do texto ASCII de <c>payload</c> com a chave <see cref="SigningKey"/> em UTF-8. Um token
/// desliga só o e-mail do tipo <c>k</c>.
/// </summary>
public sealed class UnsubscribeOptions
{
    public const string SectionName = "Notifications:Unsubscribe";

    public string SigningKey { get; set; } = string.Empty;
}
