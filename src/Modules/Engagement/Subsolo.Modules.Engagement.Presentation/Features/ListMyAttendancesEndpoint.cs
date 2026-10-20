using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.ListMyAttendances;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0029 — <c>GET /api/v1/me/attendances?period=upcoming</c> (política <c>fan</c>, rate limit <c>default</c>,
/// <c>no-store</c>): os ids dos shows em que o fã marcou "vou" e que ainda não começaram.
/// </summary>
internal sealed class ListMyAttendancesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me/attendances";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireEngagementFan(mutation: false, EngagementEndpoints.AttendanceTag)
            .WithName("ListMyAttendances")
            .WithSummary("Shows em que eu vou")
            .WithDescription(
                "Os ids dos shows em que o usuário marcou \"vou\" e que ainda não começaram (period=upcoming, o padrão).")
            .Produces<MyAttendancesResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EngagementErrors.InvalidQuery);

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        [FromQuery] string? period,
        [FromServices] IQueryHandler<ListMyAttendancesQuery, MyAttendancesResponse> handler,
        CancellationToken cancellationToken)
    {
        if (EngagementEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new ListMyAttendancesQuery(userId, period), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
