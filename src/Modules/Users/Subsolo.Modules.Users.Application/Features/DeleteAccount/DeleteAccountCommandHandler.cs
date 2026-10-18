using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.DeleteAccount;

/// <summary>
/// SPEC-0019 — confere o e-mail digitado e apaga, numa transação, o usuário, as sessões dele e os links mágicos do
/// e-mail, com <see cref="UserDeletedDomainEvent"/> na outbox; e-mail diferente →
/// <see cref="UsersErrors.DeleteConfirmationMismatch"/>, nada apagado. Conta em <c>subsolo.users.deletions</c>; o log
/// leva só o id do usuário.
/// </summary>
internal sealed partial class DeleteAccountCommandHandler(
    IUserRepository users,
    ISessionRepository sessions,
    IMagicLinkTokenRepository magicLinks,
    IUsersUnitOfWork unitOfWork,
    ILogger<DeleteAccountCommandHandler> logger)
    : ICommandHandler<DeleteAccountCommand>
{
    public async Task<Result> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UsersErrors.UserNotFoundError());
        }

        var confirmEmail = command.ConfirmEmail?.Trim();
        if (string.IsNullOrEmpty(confirmEmail) || !string.Equals(confirmEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(UsersErrors.DeleteConfirmationMismatchError());
        }

        foreach (var session in await sessions.ListByUserAsync(user.Id, cancellationToken))
        {
            sessions.Remove(session);
        }

        foreach (var token in await magicLinks.ListByEmailAsync(user.Email, cancellationToken))
        {
            magicLinks.Remove(token);
        }

        user.Delete();
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        UsersTelemetry.AccountDeleted();
        LogDeleted(logger, user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} excluiu a conta")]
    private static partial void LogDeleted(ILogger logger, Guid userId);
}
