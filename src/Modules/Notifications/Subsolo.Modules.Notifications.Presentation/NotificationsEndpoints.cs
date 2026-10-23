using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Notifications.Presentation;

/// <summary>
/// SPEC-0030 — convenções dos endpoints de avisos (copiadas do <c>EngagementEndpoints</c>): as rotas do fã (<c>fan</c>,
/// anônimo 401, <c>Cache-Control: no-store</c>, rate limit <c>default</c>; as mutações passam pelo anti-forgery do
/// host, X-CSRF) e as públicas (<c>AllowAnonymous</c>, rate limit <c>default</c>).
/// </summary>
internal static class NotificationsEndpoints
{
    public const string Tag = "Avisos";

    public static RouteHandlerBuilder RequireNoticesFan(this RouteHandlerBuilder builder, bool mutation)
    {
        builder
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithTags(Tag)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);
        return mutation
            ? builder.ProducesProblemCode(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid)
            : builder;
    }

    public static RouteHandlerBuilder RequireNoticesPublic(this RouteHandlerBuilder builder) =>
        builder
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithTags(Tag)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    /// <summary>O id do usuário da sessão validada, ou <see langword="null"/>.</summary>
    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>204 no sucesso; senão o ProblemDetails do erro.</summary>
    public static IResult NoContentOrProblem(Result result) =>
        result.IsSuccess ? Results.NoContent() : ApiResults.Problem(result);
}
