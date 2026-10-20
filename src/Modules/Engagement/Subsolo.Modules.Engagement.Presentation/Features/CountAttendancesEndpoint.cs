using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.CountAttendances;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0029 — <c>GET /api/v1/attendances/counts?showIds=a,b,c</c> (<c>AllowAnonymous</c>, rate limit <c>default</c>):
/// <c>{counts: {"&lt;showId&gt;": n}}</c>, só números (ADR-0008); até 100 ids; mais, ou id inválido → 400
/// <c>engagement.invalid_query</c>.
/// </summary>
internal sealed class CountAttendancesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/attendances/counts";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithTags(EngagementEndpoints.AttendanceTag)
            .WithName("CountAttendances")
            .WithSummary("Quantos vão")
            .WithDescription(
                "Quantos marcaram \"vou\" em cada show (até 100 ids separados por vírgula; só os números, nunca quem vai).")
            .Produces<AttendanceCountsResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EngagementErrors.InvalidQuery)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? showIds,
        [FromServices] IQueryHandler<CountAttendancesQuery, AttendanceCountsResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CountAttendancesQuery(showIds), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
