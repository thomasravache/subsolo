using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Features.SignOut;

/// <summary>
/// SPEC-0014 — revoga a sessão do token (grava <c>revoked_at_utc</c>) e conta em <c>subsolo.users.sessions_revoked</c>;
/// sessão inexistente ou já revogada não é erro (o cookie é apagado de qualquer forma). O log leva só o id do usuário.
/// </summary>
internal sealed partial class SignOutCommandHandler(
    ISessionRepository sessions,
    IUsersUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<SignOutCommandHandler> logger)
    : ICommandHandler<SignOutCommand>
{
    public async Task<Result> Handle(SignOutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var session = await sessions.GetByIdHashAsync(Session.HashToken(command.SessionToken), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return Result.Success();
        }

        session.Revoke(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        UsersTelemetry.SessionRevoked();
        LogSignedOut(logger, session.UserId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} saiu: sessão revogada")]
    private static partial void LogSignedOut(ILogger logger, Guid userId);
}
