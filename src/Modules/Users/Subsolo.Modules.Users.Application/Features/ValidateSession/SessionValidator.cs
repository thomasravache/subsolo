using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.ValidateSession;

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
    public async Task<SessionPrincipal?> ValidateAsync(string sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var session = await sessions.GetByIdHashAsync(Session.HashToken(sessionToken), cancellationToken);
        if (session is null || !session.IsActive(now))
        {
            return null;
        }

        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (session.Touch(now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new SessionPrincipal(user.Id, [.. user.Roles]);
    }
}
