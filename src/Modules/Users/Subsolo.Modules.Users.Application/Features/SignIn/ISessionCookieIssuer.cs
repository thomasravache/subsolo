namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>
/// SPEC-0014 — emite o cookie <c>__Host-subsolo</c> da resposta atual com o token da sessão (256 bits aleatórios, em
/// base64url; Emenda v2) e o id do usuário.
/// </summary>
public interface ISessionCookieIssuer
{
    Task IssueAsync(string sessionToken, Guid userId, CancellationToken cancellationToken);
}
