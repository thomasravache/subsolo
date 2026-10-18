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

namespace Subsolo.Modules.Events.Presentation.Features.CreateCity;

/// <summary>
/// SPEC-0017 — <c>POST /api/v1/admin/cities</c> (política <c>admin</c>, X-CSRF): cria a cidade; sem slug, ele é gerado
/// do nome. 201 com <c>Location: /api/v1/admin/cities/{slug}</c>.
/// </summary>
internal sealed class CreateCityEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/cities";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireEventsAdmin(mutation: true)
            .WithName("CreateCity")
            .WithSummary("Criar cidade")
            .WithDescription(
                "Cria a cidade com nome (2 a 60 caracteres), UF, fuso IANA e endereço (slug); sem slug, ele é gerado do " +
                "nome. O endereço não muda depois. Exige o cabeçalho X-CSRF.")
            .Produces<AdminCityResponse>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidCity)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.CitySlugReserved))
            .ProducesProblemCode(StatusCodes.Status409Conflict, EventsErrors.CitySlugTaken);

    private static async Task<IResult> HandleAsync(
        CreateCityRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<CreateCityCommand> validator,
        [FromServices] ICommandHandler<CreateCityCommand, AdminCityResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateCityCommand(
            request.Name, request.State, request.TimeZone, request.Slug, AdminEndpoints.AdminId(user));
        if (await EndpointValidation.ValidateAsync(validator, command, "Dados da cidade inválidos.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"{Route}/{result.Value.Slug}", result.Value)
            : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0017 — corpo da criação de cidade: nome, UF, fuso IANA e o slug opcional (<see langword="null"/> → gerado do nome).</summary>
internal sealed record CreateCityRequest(string Name, string State, string TimeZone, string? Slug);
