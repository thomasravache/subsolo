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
using Subsolo.Modules.Profiles.Application.Features.ClaimProfile;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/profiles/{id}/claims</c> (política <c>fan</c>, X-CSRF, rate limit <c>report</c>): o fã
/// reivindica um perfil sem dono com a prova (vista só pelo admin).
/// </summary>
internal sealed class ClaimProfileEndpoint : IEndpoint
{
    public const string Route = "/api/v1/profiles/{id:guid}/claims";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireProfilesFan(mutation: true, RateLimitPolicies.Report)
            .WithName("ClaimProfile")
            .WithSummary("Reivindicar perfil")
            .WithDescription(
                "Pede para ser o dono de um perfil aprovado e sem dono, com a prova (3 a 300 caracteres, vista só pelo " +
                "admin). Um pedido em análise por vez. Exige o cabeçalho X-CSRF.")
            .Produces<ProfileRequestCreated>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ProfilesErrors.InvalidRequest)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ProfilesErrors.NotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, ProfilesErrors.AlreadyOwned)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status409Conflict, ProfilesErrors.RequestPending));

    private static async Task<IResult> HandleAsync(
        Guid id,
        ClaimProfileRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<ClaimProfileCommand> validator,
        [FromServices] ICommandHandler<ClaimProfileCommand, ProfileRequestCreated> handler,
        CancellationToken cancellationToken)
    {
        if (ProfilesEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new ClaimProfileCommand(userId, id, request.Proof);
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

/// <summary>SPEC-0021 — corpo da reivindicação: a prova de que o perfil é do fã.</summary>
internal sealed record ClaimProfileRequest(string Proof);
