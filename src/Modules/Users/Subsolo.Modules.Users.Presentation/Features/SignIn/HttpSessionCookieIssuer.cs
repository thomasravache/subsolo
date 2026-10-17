using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application.Features.SignIn;

namespace Subsolo.Modules.Users.Presentation.Features.SignIn;

/// <summary>
/// SPEC-0014 — emite o cookie de sessão (<see cref="SessionAuthentication.Scheme"/>) na resposta atual, persistente e
/// só com o id do usuário e o token da sessão; prazo, renovação e proteção são os do esquema configurado no host.
/// </summary>
internal sealed class HttpSessionCookieIssuer(IHttpContextAccessor httpContextAccessor) : ISessionCookieIssuer
{
    public Task IssueAsync(string sessionToken, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionToken);
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("O cookie de sessão só pode ser emitido durante uma requisição HTTP.");
        var identity = new ClaimsIdentity(
            [
                new Claim(SessionAuthentication.UserIdClaim, userId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(SessionAuthentication.SessionTokenClaim, sessionToken),
            ],
            SessionAuthentication.Scheme);
        return httpContext.SignInAsync(
            SessionAuthentication.Scheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
