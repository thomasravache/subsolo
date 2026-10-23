using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.IntegrationEvents;

/// <summary>
/// SPEC-0018 — API pública do módulo Notifications: o único caminho para outro módulo mandar e-mail (ADR-0014). O
/// chamador passa só o modelo e os dados tipados, nunca HTML. Falhas voltam como <see cref="Result"/> com um dos
/// códigos de <see cref="NotificationsErrors"/>, sem exceção.
/// </summary>
public interface INotificationsApi
{
    Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// SPEC-0030 — enfileira o aviso <paramref name="notice"/> para <paramref name="userIds"/>, respeitando as
    /// preferências e os canais disponíveis de cada um; idempotente por (usuário, <see cref="Notice.DedupeKey"/>):
    /// chamar de novo com a mesma chave não cria entregas novas. A entrega sai pela outbox do módulo, com novas tentativas.
    /// </summary>
    Task<Result> EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct);

    /// <summary>SPEC-0030 — apaga tudo de <paramref name="userId"/> no schema <c>notifications</c> (exclusão da conta, chamada pelo Users).</summary>
    Task ForgetUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// SPEC-0030 — o que o módulo guarda de <paramref name="userId"/>, para a seção <c>notifications</c> do export (sem as
    /// chaves de criptografia das inscrições); <see langword="null"/> = nada guardado.
    /// </summary>
    Task<object?> ExportAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// SPEC-0030 — um aviso: <see cref="Kind"/> (<c>new_show</c> | <c>reminder</c> | <c>digest</c>), a chave de idempotência
/// <see cref="DedupeKey"/>, o título, o corpo e a URL que a notificação abre.
/// </summary>
public sealed record Notice(string Kind, string DedupeKey, string Title, string Body, string Url);

/// <summary>
/// SPEC-0018 — pedido de e-mail transacional: destinatário, modelo fechado e os dados do modelo
/// (<see cref="EmailTemplate.MagicLink"/>: <c>"link"</c> e <c>"minutes"</c>).
/// </summary>
public sealed record EmailRequest(string To, EmailTemplate Template, IReadOnlyDictionary<string, string> Data);

/// <summary>SPEC-0018 — modelos de e-mail (fechados).</summary>
public enum EmailTemplate
{
    /// <summary>
    /// Link mágico de login. Dados obrigatórios: <c>"link"</c> (URL absoluta https, ou http só em Development) e
    /// <c>"minutes"</c> (inteiro de 1 a 60).
    /// </summary>
    MagicLink,
}
