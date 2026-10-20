using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Site.Application.Features.ListSupporters;

namespace Subsolo.Modules.Site.Presentation.Features;

/// <summary>SPEC-0028 — <c>GET /api/v1/supporters</c>, público (<c>AllowAnonymous</c>, rate limit <c>default</c>): os apoiadores, na ordem de cadastro.</summary>
internal sealed class ListSupportersEndpoint : IEndpoint
{
    public const string Route = "/api/v1/supporters";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireSitePublic()
            .WithName("ListSupporters")
            .WithSummary("Apoiadores")
            .WithDescription("Os apoiadores do Subsolo (página Apoie), na ordem de cadastro: id, nome e link https (ou null).")
            .Produces<IReadOnlyList<SupporterResponse>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListSupportersQuery, IReadOnlyList<SupporterResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListSupportersQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
