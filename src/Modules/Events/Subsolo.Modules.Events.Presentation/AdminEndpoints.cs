using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Events.Presentation;

/// <summary>
/// SPEC-0017 — convenções dos endpoints <c>/api/v1/admin/**</c> do módulo Events: política <c>admin</c> (anônimo 401,
/// sem o papel 403), rate limit <c>default</c>, <c>Cache-Control: no-store</c> e os erros comuns documentados; as
/// mutações passam pelo anti-forgery do host (X-CSRF).
/// </summary>
internal static class AdminEndpoints
{
    public const string Tag = "Admin";

    public static RouteHandlerBuilder RequireEventsAdmin(this RouteHandlerBuilder builder, bool mutation)
    {
        builder
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithTags(Tag)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status403Forbidden, SecurityProblems.AuthForbidden)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);
        return mutation
            ? builder.WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            : builder;
    }

    /// <summary>O id do admin da sessão (para o log de auditoria), ou <see langword="null"/>.</summary>
    public static Guid? AdminId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
