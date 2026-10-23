namespace Subsolo.Modules.Notifications.IntegrationEvents;

/// <summary>
/// SPEC-0030 — o e-mail dos destinatários dos avisos, definido no contrato do Notifications e implementado pelo módulo
/// Users (ADR-0001/ADR-0014: o Notifications não depende de módulo de domínio nem guarda o e-mail).
/// </summary>
public interface IRecipientDirectory
{
    /// <summary>O e-mail de cada usuário de <paramref name="userIds"/> que existe (os inexistentes ficam de fora).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
