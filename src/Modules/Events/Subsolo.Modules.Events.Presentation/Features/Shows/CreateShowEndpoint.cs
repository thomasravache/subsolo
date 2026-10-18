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

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — <c>POST /api/v1/shows</c> (artista dono do organizador ou admin, X-CSRF, rate limit <c>default</c>): 201
/// <c>{id, slug}</c> com <c>Location: /api/v1/shows/{id}</c>.
/// </summary>
internal sealed class CreateShowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ShowsEndpoints.ShowsRoute, HandleAsync)
            .RequireShowsArtist(mutation: true)
            .WithName("CreateShow")
            .WithSummary("Cadastrar show")
            .WithDescription(
                "Cadastra o show de um perfil de que o usuário é dono (o admin, de qualquer perfil): nome (2 a 100), " +
                "cidade, início na hora local da cidade (aaaa-mm-ddThh:mm, no futuro), casa (perfil de casa ou nome de 2 " +
                "a 80), line-up de 1 a 15 bandas (perfil de banda ou nome), 1 a 5 estilos, preço (até 40), link do " +
                "ingresso https (opcional) e o flyer enviado antes. O show entra na hora na agenda da cidade. Exige o " +
                "cabeçalho X-CSRF.")
            .Produces<ShowSaved>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidShow)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.FlyerNotFound))
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.ShowSlugTaken);

    private static async Task<IResult> HandleAsync(
        ShowRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<CreateShowCommand> validator,
        [FromServices] ICommandHandler<CreateShowCommand, ShowSaved> handler,
        CancellationToken cancellationToken)
    {
        if (ShowsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new CreateShowCommand(
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
        return result.IsSuccess
            ? Results.Created($"{ShowsEndpoints.ShowsRoute}/{result.Value.Id}", result.Value)
            : ApiResults.Problem(result);
    }
}
