using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Profiles.Application.Features.ListProfileRequests;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>GET /api/v1/admin/profile-requests</c> (política <c>admin</c>, <c>no-store</c>): a fila de pedidos de
/// perfil, pendentes e mais antigos primeiro, com o contato ou a prova e o e-mail de quem pediu.
/// </summary>
internal sealed class ListProfileRequestsEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/profile-requests";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireProfilesAdmin(mutation: false)
            .WithName("ListProfileRequests")
            .WithSummary("Fila de pedidos de perfil")
            .WithDescription(
                "Pedidos de perfil e reivindicações em análise, mais antigos primeiro, com os dados do perfil, o " +
                "contato ou a prova e o e-mail de quem pediu (visíveis só aqui).")
            .Produces<IReadOnlyList<ProfileRequestQueueItem>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListProfileRequestsQuery, IReadOnlyList<ProfileRequestQueueItem>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListProfileRequestsQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
