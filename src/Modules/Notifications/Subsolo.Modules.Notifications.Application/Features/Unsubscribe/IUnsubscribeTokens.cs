namespace Subsolo.Modules.Notifications.Application.Features.Unsubscribe;

/// <summary>
/// SPEC-0030 — o token de descadastro do link de todo e-mail de aviso (§6, Emenda de 2026-10-23): assinado (HMAC-SHA256
/// com <c>Notifications:Unsubscribe:SigningKey</c>), sem login, e de um tipo só (<c>digest</c> | <c>reminder</c>).
/// </summary>
internal interface IUnsubscribeTokens
{
    /// <summary>O token de <paramref name="userId"/> para o tipo <paramref name="kind"/>, ou <see langword="null"/> sem chave configurada.</summary>
    string? Create(Guid userId, string kind);

    /// <summary>Usuário e tipo de um token íntegro, ou <see langword="null"/> (formato, assinatura ou tipo inválidos).</summary>
    UnsubscribeTarget? Read(string? token);
}

/// <summary>SPEC-0030 — o que um token de descadastro desliga: o e-mail do tipo <see cref="Kind"/> de <see cref="UserId"/>.</summary>
internal sealed record UnsubscribeTarget(Guid UserId, string Kind);
