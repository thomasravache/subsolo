using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/profiles/requests</c> (política <c>fan</c>, X-CSRF, rate limit <c>report</c>): o fã pede
/// o perfil de banda, casa ou produtor, com o contato para a aprovação (visto só pelo admin).
/// </summary>
internal sealed class RequestProfileEndpoint : IEndpoint
{
    public const string Route = "/api/v1/profiles/requests";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireProfilesFan(mutation: true, RateLimitPolicies.Report)
            .WithName("RequestProfile")
            .WithSummary("Pedir perfil")
            .WithDescription(
                "Pede o perfil (banda, casa ou produtor) para a aprovação do admin: nome de 2 a 80 caracteres, cidade, " +
                "estilos só de banda (1 a 5), links https do Instagram, do Bandcamp e do Metal Archives e o contato " +
                "(3 a 200 caracteres, visto só pelo admin). Um pedido em análise por vez. Exige o cabeçalho X-CSRF.")
            .Produces<ProfileRequestCreated>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ProfilesErrors.InvalidRequest)
            .ProducesProblemCode(StatusCodes.Status409Conflict, ProfilesErrors.RequestPending)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status409Conflict, ProfilesErrors.NameTaken));

    private static async Task<IResult> HandleAsync(
        RequestProfileRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<RequestProfileCommand> validator,
        [FromServices] ICommandHandler<RequestProfileCommand, ProfileRequestCreated> handler,
        CancellationToken cancellationToken)
    {
        if (ProfilesEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new RequestProfileCommand(
            userId, request.Type, request.Name, request.City, request.Styles, request.Links.ToInput(), request.Contact);
        if (await ProfilesEndpoints.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created((string?)null, result.Value)
            : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0021 — corpo do pedido de perfil.</summary>
internal sealed record RequestProfileRequest(
    string? Type,
    string? Name,
    string? City,
    IReadOnlyList<string>? Styles,
    ProfileLinksRequest Links,
    string? Contact);
