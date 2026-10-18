namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0021 (Emenda v2) — leitura dos e-mails de usuários por outros módulos (ADR-0001: interface pública em
/// IntegrationEvents, nunca o banco do Users). A fila do admin do Profiles mostra o e-mail de quem pediu o perfil.
/// </summary>
public interface IUsersDirectory
{
    /// <summary>O e-mail de cada usuário de <paramref name="userIds"/> que existe (os inexistentes ficam de fora).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
