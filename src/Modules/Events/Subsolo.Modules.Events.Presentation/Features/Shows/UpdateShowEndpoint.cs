using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Application.Features.UpdateShow;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — <c>PUT /api/v1/shows/{id}</c> (dono do organizador ou admin, X-CSRF): as mesmas regras do cadastro; o slug
/// não muda. 200 <c>{id, slug}</c>.
/// </summary>
internal sealed class UpdateShowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{ShowsEndpoints.ShowsRoute}/{{id:guid}}", HandleAsync)
            .RequireShowsArtist(mutation: true)
            .WithName("UpdateShow")
            .WithSummary("Editar show")
            .WithDescription(
                "Troca os dados do show (as mesmas regras do cadastro); o endereço (slug) não muda. Só o dono do perfil " +
                "organizador ou o admin. Exige o cabeçalho X-CSRF.")
            .Produces<ShowSaved>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidShow)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.FlyerNotFound))
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.ShowNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.ShowSlugTaken);

    private static async Task<IResult> HandleAsync(
        Guid id,
        ShowRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<UpdateShowCommand> validator,
        [FromServices] ICommandHandler<UpdateShowCommand, ShowSaved> handler,
        CancellationToken cancellationToken)
    {
        if (ShowsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new UpdateShowCommand(
            id,
            userId,
            ShowsEndpoints.IsAdmin(user),
            request.OrganizerProfileId,
            request.Title,
            request.City,
            request.StartsLocal,
            request.Venue.ToInput(),
            request.Lineup.ToInput(),
            request.Styles,
            request.Price,
            request.TicketUrl,
            request.FlyerId);
        if (await EndpointValidation.ValidateAsync(validator, command, "Dados do show inválidos.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
