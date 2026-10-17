using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application.Features.ListCities;

namespace Subsolo.Modules.Events.Presentation.Features.ListCities;

/// <summary>SPEC-0011 — <c>GET /api/v1/cities</c>, público (<c>AllowAnonymous</c>, rate limit <c>default</c> — SPEC-0014): as cidades do seletor de cidade.</summary>
internal sealed class ListCitiesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("ListCities")
            .WithTags("Cidades")
            .WithSummary("Cidades com agenda")
            .WithDescription("Cidades cadastradas no Subsolo, como slug e nome, ordenadas pelo nome.")
            .Produces<IReadOnlyList<CitySummaryResponse>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListCitiesQuery, IReadOnlyList<CitySummaryResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCitiesQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
