using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application.Features.CreateShow;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — convenções dos endpoints de show: artista ou admin (anônimo 401, fã 403; a posse do perfil organizador é
/// conferida no caso de uso — ADR-0003), rate limit <c>default</c>, <c>Cache-Control: no-store</c> e os erros comuns
/// documentados; as mutações passam pelo anti-forgery do host (X-CSRF).
/// </summary>
internal static class ShowsEndpoints
{
    public const string Tag = "Shows";

    public const string ShowsRoute = "/api/v1/shows";

    public static RouteHandlerBuilder RequireShowsArtist(this RouteHandlerBuilder builder, bool mutation)
    {
        builder
            .RequireAuthorization(policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthorizationPolicies.Artist, AuthorizationPolicies.Admin))
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

    /// <summary>O id do usuário da sessão validada, ou <see langword="null"/>.</summary>
    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>A sessão tem o papel <c>admin</c> (pode mexer em show de qualquer perfil).</summary>
    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(AuthorizationPolicies.Admin);

    public static ShowVenueInput? ToInput(this ShowVenueRequest? venue) =>
        venue is null ? null : new ShowVenueInput(venue.ProfileId, venue.Name);

    public static IReadOnlyList<ShowLineupItemInput>? ToInput(this IReadOnlyList<ShowLineupItemRequest?>? lineup) =>
        lineup is null ? null : [.. lineup.Select(item => new ShowLineupItemInput(item?.ProfileId, item?.Name))];
}

/// <summary>SPEC-0023 — corpo do cadastro e da edição de show (§6).</summary>
internal sealed record ShowRequest(
    Guid OrganizerProfileId,
    string? Title,
    string? City,
    string? StartsLocal,
    ShowVenueRequest? Venue,
    IReadOnlyList<ShowLineupItemRequest?>? Lineup,
    IReadOnlyList<string>? Styles,
    string? Price,
    string? TicketUrl,
    string? FlyerId);

/// <summary>SPEC-0023 — a casa: <c>{ profileId }</c> (perfil de casa) ou <c>{ name }</c> (2 a 80 caracteres).</summary>
internal sealed record ShowVenueRequest(Guid? ProfileId, string? Name);

/// <summary>SPEC-0023 — uma banda do line-up: <c>{ profileId }</c> (perfil de banda) ou <c>{ name }</c> (1 a 80 caracteres).</summary>
internal sealed record ShowLineupItemRequest(Guid? ProfileId, string? Name);
