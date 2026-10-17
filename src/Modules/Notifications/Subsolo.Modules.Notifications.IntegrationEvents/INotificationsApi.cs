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
}

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
