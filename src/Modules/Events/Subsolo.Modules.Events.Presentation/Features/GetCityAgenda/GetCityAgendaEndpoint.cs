using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

/// <summary>SPEC-0008 — <c>GET /api/v1/cities/{slug}/agenda?from=&amp;to=</c>, público (<c>AllowAnonymous</c>).</summary>
internal sealed class GetCityAgendaEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities/{slug}/agenda";

    public void MapEndpoint(IEndpointRouteBuilder app) => throw new NotImplementedException();
}
