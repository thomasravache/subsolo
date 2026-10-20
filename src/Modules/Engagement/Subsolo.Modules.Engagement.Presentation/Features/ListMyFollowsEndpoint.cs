using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application.Features.ListMyFollows;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0027 — <c>GET /api/v1/me/follows</c> (política <c>fan</c>, rate limit <c>default</c>, <c>no-store</c>): as
/// cidades e os perfis que o fã segue, com nome e endereço.
/// </summary>
internal sealed class ListMyFollowsEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me/follows";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireEngagementFan(mutation: false)
            .WithName("ListMyFollows")
            .WithSummary("O que eu sigo")
            .WithDescription(
                "As cidades (slug e nome) e os perfis (id, tipo, nome e slug) que o usuário segue, do mais antigo para o " +
                "mais novo.")
            .Produces<MyFollowsResponse>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<ListMyFollowsQuery, MyFollowsResponse> handler,
        CancellationToken cancellationToken)
    {
        if (EngagementEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new ListMyFollowsQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
