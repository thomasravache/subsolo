using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Features.HideShow;

namespace Subsolo.Modules.Moderation.Presentation.Features;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/admin/reports/{showId}/hide</c> (política <c>admin</c>, X-CSRF): oculta o show (sai da
/// agenda, da página e dos avisos) e resolve todas as denúncias pendentes dele, com a nota interna opcional. 204.
/// </summary>
internal sealed class HideShowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ModerationEndpoints.AdminReportsRoute}/{{showId:guid}}/hide", HandleAsync)
            .RequireModerationAdmin(mutation: true)
            .WithName("HideReportedShow")
            .WithSummary("Ocultar show denunciado")
            .WithDescription(
                "Oculta o show: ele sai da agenda, das páginas públicas e dos avisos; o dono o vê em Meus shows com o " +
                "selo, sem poder editar. Resolve todas as denúncias pendentes dele. A nota (até 200 caracteres) é " +
                "interna e não sai pela API. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ModerationErrors.InvalidNote)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ModerationErrors.NoPendingReports);

    private static async Task<IResult> HandleAsync(
        Guid showId,
        HideShowRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<HideShowCommand> validator,
        [FromServices] ICommandHandler<HideShowCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModerationEndpoints.UserId(user) is not { } adminId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new HideShowCommand(showId, adminId, request.Note);
        if (await EndpointValidation.ValidateAsync(validator, command, "Nota inválida.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0033 — corpo do ocultar: a nota interna opcional.</summary>
internal sealed record HideShowRequest(string? Note);
