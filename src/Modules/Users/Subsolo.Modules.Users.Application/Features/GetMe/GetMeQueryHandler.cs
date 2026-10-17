using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.GetMe;

/// <summary>SPEC-0014 — lê a conta do usuário pelo id; usuário apagado → <see cref="UsersErrors.UserNotFound"/>.</summary>
internal sealed class GetMeQueryHandler(IUserRepository users) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await users.GetByIdAsync(query.UserId, cancellationToken);
        return user is null
            ? Result.Failure<MeResponse>(UsersErrors.UserNotFoundError())
            : Result.Success(new MeResponse(user.Id, user.Email, user.DisplayName, [.. user.Roles]));
    }
}
