using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Features.KeepShow;

namespace Subsolo.Modules.Moderation.Presentation.Features;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/admin/reports/{showId}/keep</c> (política <c>admin</c>, X-CSRF): mantém o show e arquiva
/// todas as denúncias pendentes dele. 204.
/// </summary>
internal sealed class KeepShowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ModerationEndpoints.AdminReportsRoute}/{{showId:guid}}/keep", HandleAsync)
            .RequireModerationAdmin(mutation: true)
            .WithName("KeepReportedShow")
            .WithSummary("Manter show denunciado")
            .WithDescription(
                "Mantém o show na agenda e arquiva todas as denúncias pendentes dele (saem da fila). Exige o cabeçalho " +
                "X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            // SPEC-0037 (H33-04): o código security.csrf_invalid do 400 vem do RequireModerationAdmin(mutation: true).
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ModerationErrors.NoPendingReports);

    private static async Task<IResult> HandleAsync(
        Guid showId,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<KeepShowCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModerationEndpoints.UserId(user) is not { } adminId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new KeepShowCommand(showId, adminId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(result);
    }
}
