using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application.Features.SignOut;

namespace Subsolo.Modules.Users.Presentation.Features.SignOut;

/// <summary>
/// SPEC-0014 — <c>POST /api/v1/auth/logout</c> (política <c>fan</c>, X-CSRF pelo filtro do host, rate limit
/// <c>default</c>, <c>Cache-Control: no-store</c>): revoga a sessão no servidor e apaga o cookie <c>__Host-subsolo</c>.
/// </summary>
internal sealed class SignOutEndpoint : IEndpoint
{
    public const string Route = "/api/v1/auth/logout";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithName("Logout")
            .WithTags("Conta")
            .WithSummary("Sair")
            .WithDescription("Revoga a sessão atual no servidor e apaga o cookie de sessão. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] ICommandHandler<SignOutCommand> handler,
        CancellationToken cancellationToken)
    {
        var token = httpContext.User.FindFirstValue(SessionAuthentication.SessionTokenClaim);
        if (string.IsNullOrEmpty(token))
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new SignOutCommand(token), cancellationToken);
        if (result.IsFailure)
        {
            return ApiResults.Problem(result);
        }

        await httpContext.SignOutAsync(SessionAuthentication.Scheme);
        return Results.NoContent();
    }
}
