namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0014 — validação da sessão do cookie em cada requisição autenticada (chamada pelo host no
/// <c>OnValidatePrincipal</c>). <see langword="null"/> = sessão inexistente, revogada ou expirada (30 dias sem uso ou
/// 90 dias desde a entrada).
/// </summary>
public interface ISessionValidator
{
    Task<SessionPrincipal?> ValidateAsync(Guid sessionId, CancellationToken cancellationToken);
}

/// <summary>SPEC-0014 — o usuário da sessão válida e os papéis atuais dele.</summary>
public sealed record SessionPrincipal(Guid UserId, IReadOnlyList<string> Roles);
