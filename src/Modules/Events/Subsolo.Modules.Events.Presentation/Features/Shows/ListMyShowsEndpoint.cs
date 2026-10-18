using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.ListMyShows;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0023 — <c>GET /api/v1/me/shows?period=upcoming|past</c> (artista ou admin, rate limit <c>default</c>,
/// <c>no-store</c>): "Meus shows".
/// </summary>
internal sealed class ListMyShowsEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me/shows";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireShowsArtist(mutation: false)
            .WithName("ListMyShows")
            .WithSummary("Meus shows")
            .WithDescription(
                "Os shows do artista (organizados por um perfil dele) ou, para o admin, os que ele cadastrou: os " +
                "próximos (period=upcoming, o padrão, por data) ou os passados (period=past, mais recentes primeiro).")
            .Produces<IReadOnlyList<MyShowResponse>>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidPeriod);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? period,
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<ListMyShowsQuery, IReadOnlyList<MyShowResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (ShowsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new ListMyShowsQuery(userId, ShowsEndpoints.IsAdmin(user), period), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
