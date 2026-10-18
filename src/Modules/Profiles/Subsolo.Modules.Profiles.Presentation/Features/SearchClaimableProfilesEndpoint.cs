using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Profiles.Application.Features.SearchClaimableProfiles;

namespace Subsolo.Modules.Profiles.Presentation.Features;

/// <summary>
/// SPEC-0021 — <c>GET /api/v1/profiles/search?q=&amp;type=</c> (política <c>fan</c>, rate limit <c>default</c>): perfis
/// aprovados sem dono para reivindicar, até 20.
/// </summary>
internal sealed class SearchClaimableProfilesEndpoint : IEndpoint
{
    public const string Route = "/api/v1/profiles/search";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireProfilesFan(mutation: false)
            .WithName("SearchClaimableProfiles")
            .WithSummary("Buscar perfil para reivindicar")
            .WithDescription(
                "Perfis aprovados e sem dono cujo nome contém q (ao menos 2 caracteres), do tipo informado (band, " +
                "venue ou producer), até 20, por nome.")
            .Produces<IReadOnlyList<ClaimableProfileResponse>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromServices] IQueryHandler<SearchClaimableProfilesQuery, IReadOnlyList<ClaimableProfileResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SearchClaimableProfilesQuery(q, type), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
