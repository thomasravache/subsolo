using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Presentation.Features.ListStyles;

/// <summary>SPEC-0017 — <c>GET /api/v1/styles</c>, público (<c>AllowAnonymous</c>, rate limit <c>default</c>): os estilos, por nome.</summary>
internal sealed class ListStylesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/styles";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("ListStyles")
            .WithTags("Estilos")
            .WithSummary("Estilos")
            .WithDescription("Estilos de metal usados nos shows e nos filtros, como slug e nome, ordenados pelo nome.")
            .Produces<IReadOnlyList<StyleResponse>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListStylesQuery, IReadOnlyList<StyleResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListStylesQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
