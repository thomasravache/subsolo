using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.LookupProfiles;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0026 (H23-08, Emenda v2 a) — <c>GET /api/v1/profiles/lookup?q=&amp;type=</c> (artista ou admin: anônimo 401, fã
/// 403; rate limit <c>default</c>, <c>no-store</c>): a busca de perfis do cadastro de show — qualquer perfil aprovado,
/// com ou sem dono, até 20. O caso de uso do show continua recusando, para o artista, organizador de outro dono.
/// </summary>
internal sealed class LookupProfilesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/profiles/lookup";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireAuthorization(policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthorizationPolicies.Artist, AuthorizationPolicies.Admin))
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithTags(ProfilesEndpoints.FanTag)
            .WithName("LookupProfiles")
            .WithSummary("Buscar perfil para o show")
            .WithDescription(
                "Perfis aprovados, com ou sem dono, cujo nome contém q (ao menos 2 caracteres), do tipo informado (band, " +
                "venue ou producer), até 20, por nome: a casa e o organizador do cadastro de show.")
            .Produces<IReadOnlyList<ProfileLookupResponse>>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ProfilesErrors.InvalidQuery)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status403Forbidden, SecurityProblems.AuthForbidden)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromServices] IQueryHandler<LookupProfilesQuery, IReadOnlyList<ProfileLookupResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new LookupProfilesQuery(q, type), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
