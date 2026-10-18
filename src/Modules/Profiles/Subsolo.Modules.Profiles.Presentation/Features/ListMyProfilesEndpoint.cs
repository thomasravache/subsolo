using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application.Features.ListMyProfiles;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>GET /api/v1/me/profiles</c> (política <c>fan</c>, rate limit <c>default</c>, <c>no-store</c>): "Meus
/// perfis", com a situação de cada pedido e o motivo da recusa.
/// </summary>
internal sealed class ListMyProfilesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me/profiles";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireProfilesFan(mutation: false)
            .WithName("ListMyProfiles")
            .WithSummary("Meus perfis")
            .WithDescription(
                "Os perfis de que o usuário é dono e os pedidos dele (novos e reivindicações), mais novos primeiro, com " +
                "a situação (pending, approved, rejected) e o motivo da recusa.")
            .Produces<MyProfilesResponse>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<ListMyProfilesQuery, MyProfilesResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ProfilesEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new ListMyProfilesQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
