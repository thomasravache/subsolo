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
using Subsolo.Modules.Events.Application.Features.ListStyles;
using Subsolo.Modules.Events.Application.Features.RenameStyle;
using Subsolo.Modules.Events.Presentation.Features.CreateStyle;

namespace Subsolo.Modules.Events.Presentation.Features.RenameStyle;

/// <summary>SPEC-0017 — <c>PUT /api/v1/admin/styles/{slug}</c> (política <c>admin</c>, X-CSRF): renomeia o estilo; o slug não muda.</summary>
internal sealed class RenameStyleEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/styles/{slug}";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(Route, HandleAsync)
            .RequireEventsAdmin(mutation: true)
            .WithName("RenameStyle")
            .WithSummary("Renomear estilo")
            .WithDescription(
                "Troca o nome do estilo (2 a 30 caracteres); o slug não muda. Nome igual ao de outro estilo dá 409. " +
                "Exige o cabeçalho X-CSRF.")
            .Produces<StyleResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidStyle)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.StyleNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.StyleTaken);

    private static async Task<IResult> HandleAsync(
        string slug,
        StyleNameRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<RenameStyleCommand> validator,
        [FromServices] ICommandHandler<RenameStyleCommand, StyleResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new RenameStyleCommand(slug, request.Name, AdminEndpoints.AdminId(user));
        if (await EndpointValidation.ValidateAsync(validator, command, "Nome do estilo inválido.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
