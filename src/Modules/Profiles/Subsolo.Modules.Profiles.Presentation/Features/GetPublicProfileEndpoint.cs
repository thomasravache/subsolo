using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0026 — <c>GET /api/v1/profiles/{type}/{slug}</c>, público (<c>AllowAnonymous</c>, rate limit <c>default</c>): o
/// perfil aprovado das páginas <c>/banda|casa|produtor/{slug}</c>; inexistente, de outro tipo ou ainda pedido →
/// 404 <c>profiles.not_found</c>.
/// </summary>
internal sealed class GetPublicProfileEndpoint : IEndpoint
{
    public const string Route = "/api/v1/profiles/{type}/{slug}";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("GetPublicProfile")
            .WithTags(ProfilesEndpoints.FanTag)
            .WithSummary("Perfil público")
            .WithDescription(
                "O perfil aprovado do tipo (band, venue ou producer) pelo slug: nome, cidade, estilos e os links do " +
                "Instagram, do Bandcamp e do Metal Archives. Sem dono nem contato.")
            .Produces<PublicProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ProfilesErrors.NotFound)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static async Task<IResult> HandleAsync(
        string type,
        string slug,
        [FromServices] IQueryHandler<GetPublicProfileQuery, PublicProfileResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetPublicProfileQuery(type, slug), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
