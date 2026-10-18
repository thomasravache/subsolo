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
using Subsolo.Modules.Events.Application.Features.CreateCity;
using Subsolo.Modules.Events.Application.Features.UpdateCity;

namespace Subsolo.Modules.Events.Presentation.Features.UpdateCity;

/// <summary>SPEC-0017 — <c>PUT /api/v1/admin/cities/{slug}</c> (política <c>admin</c>, X-CSRF): troca nome, UF e fuso; o slug não muda.</summary>
internal sealed class UpdateCityEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/cities/{slug}";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(Route, HandleAsync)
            .RequireEventsAdmin(mutation: true)
            .WithName("UpdateCity")
            .WithSummary("Editar cidade")
            .WithDescription("Troca nome, UF e fuso da cidade; o endereço (slug) não muda. Exige o cabeçalho X-CSRF.")
            .Produces<AdminCityResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidCity)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.CityNotFound);

    private static async Task<IResult> HandleAsync(
        string slug,
        UpdateCityRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<UpdateCityCommand> validator,
        [FromServices] ICommandHandler<UpdateCityCommand, AdminCityResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCityCommand(slug, request.Name, request.State, request.TimeZone, AdminEndpoints.AdminId(user));
        if (await EndpointValidation.ValidateAsync(validator, command, "Dados da cidade inválidos.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0017 — corpo da edição de cidade: nome, UF e fuso (o slug vem da rota e não muda).</summary>
internal sealed record UpdateCityRequest(string Name, string State, string TimeZone);
