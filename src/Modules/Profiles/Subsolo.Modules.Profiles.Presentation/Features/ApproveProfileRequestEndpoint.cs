using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.ApproveProfileRequest;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/admin/profile-requests/{id}/approve</c> (política <c>admin</c>, X-CSRF): aprova o pedido;
/// o dono do perfil ganha o papel <c>artist</c> pelo evento.
/// </summary>
internal sealed class ApproveProfileRequestEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/profile-requests/{id:guid}/approve";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireProfilesAdmin(mutation: true)
            .WithName("ApproveProfileRequest")
            .WithSummary("Aprovar pedido de perfil")
            .WithDescription(
                "Aprova o pedido: o perfil novo é criado com o dono, ou o perfil reivindicado ganha o dono, que passa a " +
                "ter o papel artist. Perfil reivindicado que sumiu dá 404; nome tomado no tipo enquanto o pedido esperava dá " +
                "409. Exige o cabeçalho X-CSRF.")
            .Produces<ProfileRequestApproved>(StatusCodes.Status200OK)
            // O 400 é só o do anti-forgery (o código vem das convenções de admin).
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ProfilesErrors.RequestNotFound)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status404NotFound, ProfilesErrors.NotFound))
            .ProducesProblemCode(StatusCodes.Status409Conflict, ProfilesErrors.RequestDecided)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status409Conflict, ProfilesErrors.AlreadyOwned))
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status409Conflict, ProfilesErrors.NameTaken));

    private static async Task<IResult> HandleAsync(
        Guid id,
        [FromServices] ICommandHandler<ApproveProfileRequestCommand, ProfileRequestApproved> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ApproveProfileRequestCommand(id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
