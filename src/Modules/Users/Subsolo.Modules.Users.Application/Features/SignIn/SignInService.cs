using Microsoft.Extensions.Options;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.SignIn;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0014): as dependências são usadas pela implementação.

/// <summary>SPEC-0014 — implementação de <see cref="ISignInService"/>.</summary>
internal sealed class SignInService(
    IUserRepository users,
    ISessionRepository sessions,
    IUsersUnitOfWork unitOfWork,
    ISessionCookieIssuer cookie,
    IOptions<UsersOptions> options,
    TimeProvider clock)
    : ISignInService
{
    public Task<Result<SignInResult>> SignInAsync(string email, SignInMethod method, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0014: ISignInService.SignInAsync ainda não implementado.");
}

#pragma warning restore CS9113
