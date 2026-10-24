using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.ChangeShowStatus;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — <c>POST /api/v1/shows/{id}/status</c> (dono do organizador ou admin, X-CSRF): <c>scheduled</c>,
/// <c>cancelled</c> ou <c>postponed</c>; o show continua na agenda, com o selo. SPEC-0033: show oculto
/// pela moderação → 409 <c>events.show_hidden</c>.
/// </summary>
internal sealed class ChangeShowStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ShowsEndpoints.ShowsRoute}/{{id:guid}}/status", HandleAsync)
            .RequireShowsArtist(mutation: true)
            .WithName("ChangeShowStatus")
            .WithSummary("Mudar a situação do show")
            .WithDescription(
                "Marca o show como agendado (scheduled), cancelado (cancelled) ou adiado (postponed); cancelado e " +
                "adiado continuam na agenda com o selo. Só o dono do perfil organizador ou o admin; show oculto pela " +
                "moderação não muda de situação. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidStatus)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.ShowNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.ShowHidden);

    private static async Task<IResult> HandleAsync(
        Guid id,
        ChangeShowStatusRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<ChangeShowStatusCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ShowsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(
            new ChangeShowStatusCommand(id, userId, ShowsEndpoints.IsAdmin(user), request.Status), cancellationToken);
        return result.IsSuccess ? Results.Ok() : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0023 — corpo da mudança de situação: <c>scheduled</c>, <c>cancelled</c> ou <c>postponed</c>.</summary>
internal sealed record ChangeShowStatusRequest(string? Status);
