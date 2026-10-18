using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Application.Features.CreateCity;
using Subsolo.Modules.Events.Application.Features.ListCitiesForAdmin;

namespace Subsolo.Modules.Events.Presentation.Features.ListCitiesForAdmin;

/// <summary>SPEC-0017 — <c>GET /api/v1/admin/cities</c> (política <c>admin</c>): as cidades com UF e fuso, por nome.</summary>
internal sealed class ListCitiesForAdminEndpoint : IEndpoint
{
    public const string Route = "/api/v1/admin/cities";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireEventsAdmin(mutation: false)
            .WithName("ListCitiesForAdmin")
            .WithSummary("Cidades (admin)")
            .WithDescription("Cidades cadastradas, com slug, nome, UF e fuso, ordenadas pelo nome.")
            .Produces<IReadOnlyList<AdminCityResponse>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListCitiesForAdminQuery, IReadOnlyList<AdminCityResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCitiesForAdminQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
