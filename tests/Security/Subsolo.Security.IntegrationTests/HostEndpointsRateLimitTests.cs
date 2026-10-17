using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-10 (endpoints reais do host) — no Subsolo.Api de verdade (sem os dublês de teste), em Development e em
/// Production, todo endpoint mapeado declara uma política de rate limit (<c>RequireRateLimiting</c>), exceto
/// <c>/health</c>; o token anti-forgery, <c>/api/v1/me</c>, <c>/api/v1/auth/logout</c>, <c>/api/v1/cities</c> e a
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
    public void EveryHostEndpointExceptHealthDeclaresARateLimitPolicy(string environment)
    {
        using var factory = new SecurityApiFactory(environment, postgres.ConnectionString, withTestDoubles: false);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName).ToList();

        foreach (var route in DefaultPolicyRoutes.Append("/health"))
        {
            routes.ShouldContain(route, $"{route} não está mapeado ({environment})");
        }

        var undeclared = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText != "/health" && PolicyOf(endpoint) is null)
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

    private static string? PolicyOf(Microsoft.AspNetCore.Http.Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null
            ? endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            : null;
}
