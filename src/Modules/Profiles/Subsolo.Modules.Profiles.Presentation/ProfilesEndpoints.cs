using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;

namespace Subsolo.Modules.Profiles.Presentation;

/// <summary>
/// SPEC-0021 — convenções dos endpoints do módulo Profiles: fã (<c>fan</c>, anônimo 401) ou admin (<c>admin</c>,
/// anônimo 401, fã 403), <c>Cache-Control: no-store</c>, os erros comuns documentados e o rate limit (<c>default</c>,
/// ou <c>report</c> no pedido e na reivindicação); as mutações passam pelo anti-forgery do host (X-CSRF).
/// </summary>
internal static class ProfilesEndpoints
{
    public const string FanTag = "Perfis";

    public const string AdminTag = "Admin";

    public static RouteHandlerBuilder RequireProfilesFan(this RouteHandlerBuilder builder, bool mutation, string rateLimit = RateLimitPolicies.Default)
    {
        builder
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(rateLimit)
            .RequireNoStore()
            .WithTags(FanTag)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);
        return mutation
            ? builder.WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            : builder;
    }

    public static RouteHandlerBuilder RequireProfilesAdmin(this RouteHandlerBuilder builder, bool mutation)
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

    /// <summary>
    /// Valida no endpoint, antes do handler (como o Events, SPEC-0017): válido → <see langword="null"/>; senão, 400
    /// com <c>profiles.invalid_request</c> e os campos inválidos.
    /// </summary>
    public static async Task<IResult?> ValidateAsync<T>(IValidator<T> validator, T command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? null : ApiResults.Problem(Result.Failure(ToError(validation)));
    }

    /// <summary>Os links do corpo como o comando os recebe (o corpo pode chegar sem eles).</summary>
    public static ProfileLinksInput? ToInput(this ProfileLinksRequest? links) =>
        links is null ? null : new ProfileLinksInput(links.Instagram, links.Bandcamp, links.MetalArchives);

    private static Error ToError(ValidationResult validation)
    {
        var fields = validation.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return Error.Validation(validation.Errors[0].ErrorCode, "Dados do perfil inválidos.", fields);
    }
}

/// <summary>SPEC-0021 — links do perfil no corpo (Instagram, Bandcamp, Metal Archives; só <c>https</c> dos domínios oficiais).</summary>
internal sealed record ProfileLinksRequest(string? Instagram, string? Bandcamp, string? MetalArchives);
