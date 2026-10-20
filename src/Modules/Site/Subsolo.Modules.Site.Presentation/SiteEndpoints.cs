using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Site.Presentation;

/// <summary>
/// SPEC-0028 — convenções dos endpoints do módulo Site: os de admin (<c>/api/v1/admin/supporters</c>: política
/// <c>admin</c>, anônimo 401, sem o papel 403, rate limit <c>default</c>, <c>Cache-Control: no-store</c>; as mutações
/// passam pelo anti-forgery do host, X-CSRF) e a lista pública (<c>AllowAnonymous</c>, rate limit <c>default</c>).
/// </summary>
internal static class SiteEndpoints
{
    public const string AdminTag = "Admin";

    public const string PublicTag = "Apoiadores";

    /// <summary>
    /// Convenções de admin; o 400 do anti-forgery entra só como código (cada endpoint declara a resposta 400, que pode
    /// ter também o código de validação).
    /// </summary>
    public static RouteHandlerBuilder RequireSiteAdmin(this RouteHandlerBuilder builder) =>
        builder
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithTags(AdminTag)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status403Forbidden, SecurityProblems.AuthForbidden)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid));

    public static RouteHandlerBuilder RequireSitePublic(this RouteHandlerBuilder builder) =>
        builder
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithTags(PublicTag)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    /// <summary>O id do admin da sessão (para o log de auditoria), ou <see langword="null"/>.</summary>
    public static Guid? AdminId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>
    /// Valida <paramref name="request"/>: válido → <see langword="null"/>; senão, o ProblemDetails 400 com o código da
    /// primeira falha e as mensagens de cada campo inválido (como o <c>EndpointValidation</c> do módulo Events).
    /// </summary>
    public static async Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        string description,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (validation.IsValid)
        {
            return null;
        }

        var fields = validation.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return ApiResults.Problem(Result.Failure(Error.Validation(validation.Errors[0].ErrorCode, description, fields)));
    }
}
