using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Engagement.Application.Features.CountFollowers;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0027 — contagens públicas (<c>AllowAnonymous</c>, rate limit <c>default</c>): <c>GET
/// /api/v1/profiles/{profileId}/followers</c> e <c>GET /api/v1/cities/{slug}/followers</c> → <c>{count}</c>, só o número;
/// alvo inexistente (ou perfil não aprovado) → 404 <c>engagement.target_not_found</c>. A rota do perfil exige um
/// <c>guid</c> e o segmento literal <c>followers</c>, então não colide com <c>/api/v1/profiles/{type}/{slug}</c>.
/// </summary>
internal sealed class CountFollowersEndpoints : IEndpoint
{
    public const string ProfileRoute = "/api/v1/profiles/{profileId:guid}/followers";

    public const string CityRoute = "/api/v1/cities/{slug}/followers";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(ProfileRoute, CountProfileAsync)
            .RequireEngagementPublic()
            .WithName("CountProfileFollowers")
            .WithSummary("Seguidores do perfil")
            .WithDescription("Quantos usuários seguem o perfil aprovado (só o número, sem quem segue).")
            .Produces<FollowerCountResponse>(StatusCodes.Status200OK);

        app.MapGet(CityRoute, CountCityAsync)
            .RequireEngagementPublic()
            .WithName("CountCityFollowers")
            .WithSummary("Seguidores da cidade")
            .WithDescription("Quantos usuários seguem a cidade (só o número, sem quem segue).")
            .Produces<FollowerCountResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CountProfileAsync(
        Guid profileId,
        [FromServices] IQueryHandler<CountProfileFollowersQuery, FollowerCountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CountProfileFollowersQuery(profileId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }

    private static async Task<IResult> CountCityAsync(
        string slug,
        [FromServices] IQueryHandler<CountCityFollowersQuery, FollowerCountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CountCityFollowersQuery(slug), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
