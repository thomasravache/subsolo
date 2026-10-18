using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/admin/profile-requests/{id}/reject</c> (política <c>admin</c>, X-CSRF): recusa o pedido
/// com o motivo, que o autor vê em "Meus perfis".
/// </summary>
internal sealed class RejectProfileRequestEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/profile-requests/{id:guid}/reject";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireProfilesAdmin(mutation: true)
            .WithName("RejectProfileRequest")
            .WithSummary("Recusar pedido de perfil")
            .WithDescription(
                "Recusa o pedido com o motivo (3 a 200 caracteres), mostrado ao autor em Meus perfis; ele pode pedir de " +
                "novo. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ProfilesErrors.InvalidRequest)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ProfilesErrors.RequestNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, ProfilesErrors.RequestDecided);

    private static async Task<IResult> HandleAsync(
        Guid id,
        RejectProfileRequestRequest request,
        [FromServices] IValidator<RejectProfileRequestCommand> validator,
        [FromServices] ICommandHandler<RejectProfileRequestCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new RejectProfileRequestCommand(id, request.Reason);
        if (await ProfilesEndpoints.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.Ok() : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0021 — corpo da recusa: o motivo, mostrado ao autor do pedido.</summary>
internal sealed record RejectProfileRequestRequest(string Reason);
