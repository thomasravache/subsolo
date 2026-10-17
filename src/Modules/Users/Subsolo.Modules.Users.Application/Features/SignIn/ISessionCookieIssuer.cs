namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>SPEC-0014 — emite o cookie <c>__Host-subsolo</c> da resposta atual com o id da sessão e o id do usuário.</summary>
public interface ISessionCookieIssuer
{
    Task IssueAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);
}
