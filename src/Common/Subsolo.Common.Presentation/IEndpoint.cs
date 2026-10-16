using Microsoft.AspNetCore.Routing;

namespace Subsolo.Common.Presentation;

/// <summary>SPEC-0007 — um endpoint Minimal API de uma feature (vertical slice).</summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
