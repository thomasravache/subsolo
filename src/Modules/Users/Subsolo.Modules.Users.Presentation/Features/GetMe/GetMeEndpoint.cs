using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application.Features.GetMe;

namespace Subsolo.Modules.Users.Presentation.Features.GetMe;

/// <summary>
/// SPEC-0014 — <c>GET /api/v1/me</c>: a conta de quem está logado (política <c>fan</c>), com
/// <c>Cache-Control: no-store</c> e rate limit <c>default</c>.
/// </summary>
internal sealed class GetMeEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithName("GetMe")
            .WithTags("Conta")
            .WithSummary("Quem sou eu")
            .WithDescription("Id, e-mail, nome de exibição e papéis do usuário da sessão atual.")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<GetMeQuery, MeResponse> handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var userId))
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new GetMeQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
