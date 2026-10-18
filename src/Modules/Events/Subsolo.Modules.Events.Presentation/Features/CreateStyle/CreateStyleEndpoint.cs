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
using Subsolo.Modules.Events.Application.Features.CreateStyle;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Presentation.Features.CreateStyle;

/// <summary>SPEC-0017 — <c>POST /api/v1/admin/styles</c> (política <c>admin</c>, X-CSRF): estilo novo, com o slug gerado do nome.</summary>
internal sealed class CreateStyleEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/styles";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireEventsAdmin(mutation: true)
            .WithName("CreateStyle")
            .WithSummary("Adicionar estilo")
            .WithDescription(
                "Cria o estilo com nome de 2 a 30 caracteres; o slug é gerado do nome e não muda depois. Exige o cabeçalho X-CSRF.")
            .Produces<StyleResponse>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidStyle)
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.StyleTaken);

    private static async Task<IResult> HandleAsync(
        StyleNameRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<CreateStyleCommand> validator,
        [FromServices] ICommandHandler<CreateStyleCommand, StyleResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateStyleCommand(request.Name, AdminEndpoints.AdminId(user));
        if (await EndpointValidation.ValidateAsync(validator, command, "Nome do estilo inválido.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"{Route}/{result.Value.Slug}", result.Value)
            : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0017 — corpo da criação e da renomeação de estilo: o nome (2 a 30 caracteres).</summary>
internal sealed record StyleNameRequest(string Name);
