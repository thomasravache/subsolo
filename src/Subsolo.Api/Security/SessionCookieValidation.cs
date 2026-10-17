using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Api.Security;

/// <summary>
/// SPEC-0014 — validação do cookie de sessão em cada requisição autenticada (<c>OnValidatePrincipal</c>), delegada ao
/// <see cref="ISessionValidator"/> do módulo Users: sessão inexistente, revogada ou expirada → cookie rejeitado e
/// apagado (a requisição segue anônima e o endpoint protegido responde 401); válida → o principal passa a ter o id do
/// usuário, o token e os papéis atuais lidos da sessão. Banco indisponível → <see cref="SessionStoreUnavailableException"/>
/// (503), nunca usuário anônimo silencioso.
/// </summary>
internal static class SessionCookieValidation
{
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var token = context.Principal?.FindFirstValue(SessionAuthentication.SessionTokenClaim);
        var userId = context.Principal?.FindFirstValue(SessionAuthentication.UserIdClaim);
        var session = string.IsNullOrEmpty(token) ? null : await ValidateAsync(context.HttpContext, token);
        if (session is null || !string.Equals(userId, Format(session.UserId), StringComparison.Ordinal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        Claim[] claims =
        [
            new(SessionAuthentication.UserIdClaim, Format(session.UserId)),
            new(SessionAuthentication.SessionTokenClaim, token!),
            .. session.Roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];
        context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name)));
    }

    private static async Task<SessionPrincipal?> ValidateAsync(HttpContext httpContext, string token)
    {
        var validator = httpContext.RequestServices.GetRequiredService<ISessionValidator>();
        try
        {
            return await validator.ValidateAsync(token, httpContext.RequestAborted);
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            throw new SessionStoreUnavailableException(exception);
        }
    }

    /// <summary>Falha de acesso ao banco (o Npgsql e o EF Core embrulham <see cref="DbException"/> e timeouts).</summary>
    private static bool IsStoreFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static string Format(Guid userId) => userId.ToString("D", CultureInfo.InvariantCulture);
}

/// <summary>SPEC-0014 — o banco das sessões não respondeu durante a validação do cookie (vira 503 ProblemDetails).</summary>
internal sealed class SessionStoreUnavailableException : Exception
{
    public SessionStoreUnavailableException()
        : base("A validação da sessão não alcançou o banco.")
    {
    }

    public SessionStoreUnavailableException(string message)
        : base(message)
    {
    }

    public SessionStoreUnavailableException(Exception innerException)
        : base("A validação da sessão não alcançou o banco.", innerException)
    {
    }

    public SessionStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// SPEC-0014 — converte <see cref="SessionStoreUnavailableException"/> em 503 ProblemDetails
/// <c>code=service.unavailable</c>, sem detalhes da exceção (em qualquer ambiente).
/// </summary>
internal sealed class SessionStoreUnavailableExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SessionStoreUnavailableException)
        {
            return false;
        }

        SecurityLog.Rejected(httpContext, SecurityProblems.ServiceUnavailable);
        await SecurityProblems.Unavailable().ExecuteAsync(httpContext);
        return true;
    }
}
