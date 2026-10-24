using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Moderation.Presentation;

/// <summary>
/// SPEC-0033 — convenções dos endpoints do módulo Moderation: a denúncia é do fã (<c>fan</c>, anônimo 401) com o limite
/// <see cref="ModerationRateLimit.PolicyName"/> (10 por fã por dia); a fila e as decisões são do admin (<c>admin</c>,
/// anônimo 401, fã 403) com o limite <c>default</c>. Todas com <c>Cache-Control: no-store</c> e os erros comuns
/// documentados; as mutações passam pelo anti-forgery do host (X-CSRF).
/// </summary>
internal static class ModerationEndpoints
{
    public const string FanTag = "Denúncias";

    public const string AdminTag = "Admin";

    public const string AdminReportsRoute = "/api/v1/admin/reports";

    public static RouteHandlerBuilder RequireModerationFan(this RouteHandlerBuilder builder) =>
        builder
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(ModerationRateLimit.PolicyName)
            .RequireNoStore()
            .WithTags(FanTag)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    public static RouteHandlerBuilder RequireModerationAdmin(this RouteHandlerBuilder builder, bool mutation)
    {
        builder
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithTags(AdminTag)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status403Forbidden, SecurityProblems.AuthForbidden)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);
        return mutation
            ? builder.WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            : builder;
    }

    /// <summary>O id do usuário da sessão validada, ou <see langword="null"/>.</summary>
    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
