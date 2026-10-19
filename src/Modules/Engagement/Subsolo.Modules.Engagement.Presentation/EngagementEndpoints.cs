using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;

namespace Subsolo.Modules.Engagement.Presentation;

/// <summary>
/// SPEC-0027 — convenções dos endpoints do módulo Engagement: as rotas do fã (<c>fan</c>, anônimo 401,
/// <c>Cache-Control: no-store</c>, rate limit <c>default</c>; as mutações passam pelo anti-forgery do host, X-CSRF) e as
/// contagens públicas (<c>AllowAnonymous</c>, rate limit <c>default</c>).
/// </summary>
internal static class EngagementEndpoints
{
    public const string Tag = "Seguir";

    public static RouteHandlerBuilder RequireEngagementFan(this RouteHandlerBuilder builder, bool mutation)
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
            ? builder
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblemCode(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid)
            : builder;
    }

    public static RouteHandlerBuilder RequireEngagementPublic(this RouteHandlerBuilder builder) =>
        builder
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithTags(Tag)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EngagementErrors.TargetNotFound)
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
