using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.DeleteAccount;

/// <summary>
/// SPEC-0019 — confere o e-mail digitado e apaga, numa transação, o usuário, as sessões dele e os links mágicos do
/// e-mail, com <see cref="UserDeletedDomainEvent"/> na outbox; e-mail diferente →
/// <see cref="UsersErrors.DeleteConfirmationMismatch"/>, nada apagado. O log leva só o id do usuário.
/// </summary>
internal sealed class DeleteAccountCommandHandler(
    IUserRepository users,
    ISessionRepository sessions,
    IMagicLinkTokenRepository magicLinks,
    IUsersUnitOfWork unitOfWork,
    ILogger<DeleteAccountCommandHandler> logger)
    : ICommandHandler<DeleteAccountCommand>
{
    public Task<Result> Handle(DeleteAccountCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            $"SPEC-0019: scaffold — implementar no Green ({users}, {sessions}, {magicLinks}, {unitOfWork}, {logger}).");
}
