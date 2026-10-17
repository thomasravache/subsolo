using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-10 (endpoints reais do host) — no Subsolo.Api de verdade (sem os dublês de teste), em Development e em
/// Production, todo endpoint mapeado declara uma política de rate limit (<c>RequireRateLimiting</c>), exceto as sondas
/// <c>/health</c> e <c>/alive</c> e o <c>/openapi/*</c> de desenvolvimento (Emenda v2); o token anti-forgery, <c>/api/v1/me</c>, <c>/api/v1/auth/logout</c>, <c>/api/v1/cities</c> e a
/// agenda usam <c>default</c>. A regra com os endpoints de exemplo fica em <c>Subsolo.ArchitectureTests/Security</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class HostEndpointsRateLimitTests(PostgresFixture postgres)
{
    private static readonly string[] DefaultPolicyRoutes =
    [
        "/api/v1/antiforgery",
        "/api/v1/me",
        "/api/v1/auth/logout",
        "/api/v1/cities",
        "/api/v1/cities/{slug}/agenda",
    ];

    [Theory]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    [InlineData("Development")]
    [InlineData("Production")]
    public void EveryHostEndpointExceptProbesAndOpenApiDeclaresARateLimitPolicy(string environment)
    {
        using var factory = new SecurityApiFactory(environment, postgres.ConnectionString, withTestDoubles: false);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName).ToList();

        foreach (var route in DefaultPolicyRoutes.Append("/health").Append("/alive"))
        {
            routes.ShouldContain(route, $"{route} não está mapeado ({environment})");
        }

        var undeclared = endpoints
            .Where(endpoint => !IsExempt(endpoint.RoutePattern.RawText) && PolicyOf(endpoint) is null)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName)
            .ToList();
        undeclared.ShouldBeEmpty(
            $"endpoints sem política de rate limit ({environment}): {string.Join(", ", undeclared)}");

        foreach (var route in DefaultPolicyRoutes)
        {
            endpoints.Where(endpoint => endpoint.RoutePattern.RawText == route)
                .ShouldAllBe(endpoint => PolicyOf(endpoint) == "default", $"{route} precisa da política default ({environment})");
        }
    }

    /// <summary>Sondas <c>/health</c> e <c>/alive</c> e o documento OpenAPI (só em desenvolvimento).</summary>
    private static bool IsExempt(string? route) =>
        route is "/health" or "/alive" || route?.StartsWith("/openapi/", StringComparison.Ordinal) == true;

    private static string? PolicyOf(Microsoft.AspNetCore.Http.Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null
            ? endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            : null;
}
