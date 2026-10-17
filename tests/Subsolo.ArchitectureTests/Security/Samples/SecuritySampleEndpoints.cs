using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.ArchitectureTests.Security.Samples;

/// <summary>SPEC-0010 IT-01 — endpoint de exemplo que viola o ADR-0003: sem política nem <c>AllowAnonymous</c>.</summary>
public sealed class UndeclaredSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/security/undeclared";

    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet(Route, () => Results.Ok());
}

/// <summary>SPEC-0010 IT-01 — endpoint de exemplo público de forma explícita.</summary>
public sealed class AnonymousSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/security/anonymous";

    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet(Route, () => Results.Ok()).AllowAnonymous();
}

/// <summary>SPEC-0010 IT-01 — endpoint de exemplo protegido por uma política nomeada.</summary>
public sealed class ProtectedSampleEndpoint : IEndpoint
{
    public const string Route = "/__samples/security/admin";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, () => Results.Ok()).RequireAuthorization("admin");
}
