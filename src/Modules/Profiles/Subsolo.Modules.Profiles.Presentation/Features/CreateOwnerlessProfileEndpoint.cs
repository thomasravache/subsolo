using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.CreateOwnerlessProfile;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/admin/profiles</c> (política <c>admin</c>, X-CSRF): cria um perfil aprovado e sem dono,
/// que aparece na busca de reivindicação.
/// </summary>
internal sealed class CreateOwnerlessProfileEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/profiles";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireProfilesAdmin(mutation: true)
            .WithName("CreateOwnerlessProfile")
            .WithSummary("Criar perfil sem dono")
            .WithDescription(
                "Cria o perfil (banda, casa ou produtor) já aprovado e sem dono, com os mesmos campos do pedido e sem " +
                "o contato; o dono pode reivindicá-lo depois. Exige o cabeçalho X-CSRF.")
            .Produces<OwnerlessProfileCreated>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ProfilesErrors.InvalidRequest)
            .ProducesProblemCode(StatusCodes.Status409Conflict, ProfilesErrors.NameTaken);

    private static async Task<IResult> HandleAsync(
        CreateOwnerlessProfileRequest request,
        [FromServices] IValidator<CreateOwnerlessProfileCommand> validator,
        [FromServices] ICommandHandler<CreateOwnerlessProfileCommand, OwnerlessProfileCreated> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateOwnerlessProfileCommand(
            request.Type, request.Name, request.City, request.Styles, request.Links.ToInput());
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

/// <summary>SPEC-0021 — corpo da criação de perfil sem dono (o pedido sem o contato).</summary>
internal sealed record CreateOwnerlessProfileRequest(
    string? Type,
    string? Name,
    string? City,
    IReadOnlyList<string>? Styles,
    ProfileLinksRequest Links);
