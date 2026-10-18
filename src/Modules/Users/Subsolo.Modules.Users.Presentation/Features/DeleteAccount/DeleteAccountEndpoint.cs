using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.DeleteAccount;
using Subsolo.Modules.Users.Presentation.Features.GetMe;

namespace Subsolo.Modules.Users.Presentation.Features.DeleteAccount;

/// <summary>
/// SPEC-0019 — <c>DELETE /api/v1/me</c> (política <c>fan</c>, X-CSRF pelo filtro do host, rate limit <c>default</c>,
/// <c>Cache-Control: no-store</c>): com o e-mail da própria conta digitado, apaga a conta, as sessões e os links do
/// e-mail, publica <c>UserDeleted</c> pela outbox e apaga o cookie <c>__Host-subsolo</c>.
/// </summary>
internal sealed class DeleteAccountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete(GetMeEndpoint.Route, HandleAsync)
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithName("DeleteAccount")
            .WithTags("Conta")
            .WithSummary("Excluir minha conta")
            .WithDescription(
                "Apaga a conta, as sessões e os links de acesso pendentes, depois de o usuário digitar o e-mail da " +
                "própria conta. Não dá para desfazer. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UsersErrors.DeleteConfirmationMismatch)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        [FromBody] DeleteAccountRequest request,
        HttpContext httpContext,
        [FromServices] ICommandHandler<DeleteAccountCommand> handler,
        CancellationToken cancellationToken)
    {
        if (AccountUser.Id(httpContext.User) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new DeleteAccountCommand(userId, request.ConfirmEmail), cancellationToken);
        if (result.IsFailure)
        {
            return ApiResults.Problem(result);
        }

        await httpContext.SignOutAsync(SessionAuthentication.Scheme);
        return Results.NoContent();
    }
}

/// <summary>SPEC-0019 — corpo do <c>DELETE /api/v1/me</c>: o e-mail digitado para confirmar.</summary>
internal sealed record DeleteAccountRequest(string ConfirmEmail);
