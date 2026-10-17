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
/// SPEC-0016 (§7.6 b, e) — duas primeiras entradas simultâneas com o mesmo e-mail dão um único usuário (a segunda entra
/// na conta criada pela primeira, sem 500); o <see cref="ILogger"/> é obrigatório.
/// </summary>
internal sealed partial class SignInService(
    IUserRepository users,
    ISessionRepository sessions,
    IUsersUnitOfWork unitOfWork,
    ISessionCookieIssuer cookie,
    IOptions<UsersOptions> options,
    TimeProvider clock,
    ILogger<SignInService> logger)
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
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UserEmailTakenException) when (isNewUser)
        {
            // Primeira entrada simultânea com o mesmo e-mail (§7.6 b): a outra criou a conta antes; esta entra nela.
            user = await users.GetByEmailAsync(normalized, cancellationToken)
                ?? throw new InvalidOperationException("O índice único do e-mail recusou o usuário, mas a conta não foi encontrada.");
            user.SyncAdminRole(isAdmin);
            isNewUser = false;
            (session, token) = Session.Start(user.Id, method, now);
            sessions.Add(session);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await cookie.IssueAsync(token, user.Id, cancellationToken);

        UsersTelemetry.SignedIn(method);
        LogSignedIn(logger, user.Id, method, isNewUser);
        return Result.Success(new SignInResult(user.Id, isNewUser));
    }

    private bool IsAdmin(string normalizedEmail) =>
        options.Value.AdminEmails.Any(admin => EmailAddress.Normalize(admin) == normalizedEmail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} entrou por {SignInMethod} (novo: {IsNewUser})")]
    private static partial void LogSignedIn(ILogger logger, Guid userId, SignInMethod signInMethod, bool isNewUser);
}
