using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.ValidateSession;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0014): as dependências são usadas pela implementação.

/// <summary>
/// SPEC-0014 — implementação de <see cref="ISessionValidator"/>: sessão inexistente, revogada ou expirada (prazo
/// deslizante ou teto absoluto) → <see langword="null"/>; válida → usuário e papéis atuais, renovando o prazo no máximo
/// uma vez por hora e nunca além do teto.
/// </summary>
internal sealed class SessionValidator(
    ISessionRepository sessions,
    IUserRepository users,
    IUsersUnitOfWork unitOfWork,
    TimeProvider clock)
    : ISessionValidator
{
    public Task<SessionPrincipal?> ValidateAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0014: ISessionValidator.ValidateAsync ainda não implementado.");
}

#pragma warning restore CS9113
