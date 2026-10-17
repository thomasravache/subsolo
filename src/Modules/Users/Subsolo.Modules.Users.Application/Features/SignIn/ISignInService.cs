using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>
/// SPEC-0014 — entrada de um usuário, usada pelos slices de login (link mágico, Google): encontra ou cria o usuário
/// pelo e-mail normalizado, atualiza o papel <c>admin</c> pela configuração, cria a sessão e emite o cookie com o id
/// da sessão na resposta atual. Erro: <see cref="UsersErrors.InvalidEmail"/>.
/// </summary>
public interface ISignInService
{
    Task<Result<SignInResult>> SignInAsync(string email, SignInMethod method, CancellationToken cancellationToken);
}

/// <summary>SPEC-0014 — resultado da entrada: o usuário e se ele acabou de ser criado.</summary>
public sealed record SignInResult(Guid UserId, bool IsNewUser);
