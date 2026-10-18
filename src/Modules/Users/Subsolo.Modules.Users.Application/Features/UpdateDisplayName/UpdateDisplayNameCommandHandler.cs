using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Features.GetMe;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.UpdateDisplayName;

/// <summary>
/// SPEC-0019 — valida e grava o nome de exibição; fora da regra → <see cref="UsersErrors.InvalidDisplayName"/>, nada
/// gravado. O log leva só o id do usuário.
/// </summary>
internal sealed class UpdateDisplayNameCommandHandler(
    IUserRepository users,
    IUsersUnitOfWork unitOfWork,
    ILogger<UpdateDisplayNameCommandHandler> logger)
    : ICommandHandler<UpdateDisplayNameCommand, MeResponse>
{
    public Task<Result<MeResponse>> Handle(UpdateDisplayNameCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            $"SPEC-0019: scaffold — implementar no Green ({users}, {unitOfWork}, {logger}).");
}
