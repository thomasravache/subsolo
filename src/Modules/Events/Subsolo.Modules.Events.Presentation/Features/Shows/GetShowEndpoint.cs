using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetShow;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — <c>GET /api/v1/shows/{id}</c> (dono do organizador ou admin, <c>no-store</c>): o show para a tela "Editar
/// show", com o início na hora local da cidade.
/// </summary>
internal sealed class GetShowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{ShowsEndpoints.ShowsRoute}/{{id:guid}}", HandleAsync)
            .RequireShowsArtist(mutation: false)
            .WithName("GetShowForEdit")
            .WithSummary("Show para editar")
            .WithDescription(
                "Os dados do show como o cadastro recebe (início na hora local da cidade, casa e line-up com perfil ou " +
                "nome), a situação e o flyer. Só o dono do perfil organizador ou o admin.")
            .Produces<ShowForEditResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.ShowNotFound);

    private static async Task<IResult> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<GetShowQuery, ShowForEditResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ShowsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new GetShowQuery(id, userId, ShowsEndpoints.IsAdmin(user)), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
