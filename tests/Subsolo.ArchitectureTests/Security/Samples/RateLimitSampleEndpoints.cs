using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.ArchitectureTests.Security.Samples;

/// <summary>SPEC-0014 IT-10 — endpoint de exemplo que viola a regra: público, mas sem política de rate limit.</summary>
public sealed class UnlimitedSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/rate-limit/unlimited";

    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet(Route, () => Results.Ok()).AllowAnonymous();
}

/// <summary>SPEC-0014 IT-10 — endpoint de exemplo que desliga o rate limit (conta como sem política).</summary>
public sealed class DisabledRateLimitSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/rate-limit/disabled";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, () => Results.Ok()).AllowAnonymous().DisableRateLimiting();
}

/// <summary>SPEC-0014 IT-10 — endpoint de exemplo com a política <c>default</c>.</summary>
public sealed class LimitedSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/rate-limit/limited";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, () => Results.Ok()).AllowAnonymous().RequireRateLimiting("default");
}

/// <summary>SPEC-0014 IT-10 — <c>/health</c> de exemplo, sem política: dispensado pela regra.</summary>
public sealed class HealthSampleEndpoint : IEndpoint
{
    public const string Route = "/health";

    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet(Route, () => Results.Ok()).AllowAnonymous();
}
