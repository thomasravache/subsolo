using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.ConsumeMagicLink;

namespace Subsolo.Modules.Users.Presentation.Features.ConsumeMagicLink;

/// <summary>
/// SPEC-0016 — <c>POST /api/v1/auth/magic-link/consume</c> (público, X-CSRF pelo filtro do host, rate limit
/// <c>auth</c>, <c>Cache-Control: no-store</c>): troca o token do link pela sessão (cookie <c>__Host-subsolo</c> novo) e
/// devolve o caminho de volta saneado; inexistente, expirado ou já usado → 400 <c>users.magic_link_invalid</c>.
/// </summary>
internal sealed class ConsumeMagicLinkEndpoint : IEndpoint
{
    public const string Route = "/api/v1/auth/magic-link/consume";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .RequireNoStore()
            .WithName("ConsumeMagicLink")
            .WithTags("Conta")
            .WithSummary("Entrar pelo link de acesso")
            .WithDescription(
                "Troca o token do link de acesso pela sessão (cookie de sessão novo) e devolve o caminho de volta. O " +
                "link vale uma vez só. Exige o cabeçalho X-CSRF.")
            .Produces<ConsumeMagicLinkResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UsersErrors.MagicLinkInvalid)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static async Task<IResult> HandleAsync(
        ConsumeMagicLinkRequest request,
        [FromServices] ICommandHandler<ConsumeMagicLinkCommand, ConsumeMagicLinkResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ConsumeMagicLinkCommand(request.Token), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0016 — corpo do consumo: o token do link.</summary>
internal sealed record ConsumeMagicLinkRequest(string Token);
