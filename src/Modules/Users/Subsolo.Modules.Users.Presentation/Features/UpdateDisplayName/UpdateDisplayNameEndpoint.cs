using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.GetMe;
using Subsolo.Modules.Users.Application.Features.UpdateDisplayName;
using Subsolo.Modules.Users.Presentation.Features.GetMe;

namespace Subsolo.Modules.Users.Presentation.Features.UpdateDisplayName;

/// <summary>
/// SPEC-0019 — <c>PUT /api/v1/me</c> (política <c>fan</c>, X-CSRF pelo filtro do host, rate limit <c>default</c>,
/// <c>Cache-Control: no-store</c>): muda ou remove o nome de exibição e devolve o corpo do <c>GET /me</c>.
/// </summary>
internal sealed class UpdateDisplayNameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(GetMeEndpoint.Route, HandleAsync)
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithName("UpdateDisplayName")
            .WithTags("Conta")
            .WithSummary("Mudar o nome de exibição")
            .WithDescription(
                "Troca o nome de exibição (1 a 60 caracteres, sem espaços nas pontas); vazio ou nulo remove o nome. " +
                "Exige o cabeçalho X-CSRF.")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UsersErrors.InvalidDisplayName)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        UpdateDisplayNameRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UpdateDisplayNameCommand, MeResponse> handler,
        CancellationToken cancellationToken)
    {
        if (AccountUser.Id(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new UpdateDisplayNameCommand(userId, request.DisplayName), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0019 — corpo do <c>PUT /api/v1/me</c>: o nome novo, ou nulo/vazio para remover.</summary>
internal sealed record UpdateDisplayNameRequest(string? DisplayName);
