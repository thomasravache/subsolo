using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>
/// SPEC-0014 — implementação de <see cref="ISignInService"/>: e-mail normalizado e validado (nada é gravado se for
/// inválido), usuário encontrado ou criado, papel <c>admin</c> conforme <c>Users:AdminEmails</c>, sessão nova (token
/// novo a cada entrada) gravada com o usuário numa transação e cookie emitido. O log leva só o id do usuário.
/// </summary>
internal sealed partial class SignInService(
    IUserRepository users,
    ISessionRepository sessions,
    IUsersUnitOfWork unitOfWork,
    ISessionCookieIssuer cookie,
    IOptions<UsersOptions> options,
    TimeProvider clock,
    ILogger<SignInService>? logger = null)
    : ISignInService
{
    public async Task<Result<SignInResult>> SignInAsync(string email, SignInMethod method, CancellationToken cancellationToken)
    {
        if (EmailAddress.Normalize(email) is not { } normalized)
        {
            return Result.Failure<SignInResult>(UsersErrors.InvalidEmailError());
        }

        var now = clock.GetUtcNow();
        var isAdmin = IsAdmin(normalized);
        var user = await users.GetByEmailAsync(normalized, cancellationToken);
        var isNewUser = user is null;
        if (user is null)
        {
            user = User.Register(normalized, isAdmin, now);
            users.Add(user);
        }
        else
        {
            user.SyncAdminRole(isAdmin);
        }

        var (session, token) = Session.Start(user.Id, method, now);
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cookie.IssueAsync(token, user.Id, cancellationToken);

        UsersTelemetry.SignedIn(method);
        if (logger is not null)
        {
            LogSignedIn(logger, user.Id, method, isNewUser);
        }

        return Result.Success(new SignInResult(user.Id, isNewUser));
    }

    private bool IsAdmin(string normalizedEmail) =>
        options.Value.AdminEmails.Any(admin => EmailAddress.Normalize(admin) == normalizedEmail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} entrou por {SignInMethod} (novo: {IsNewUser})")]
    private static partial void LogSignedIn(ILogger logger, Guid userId, SignInMethod signInMethod, bool isNewUser);
}
