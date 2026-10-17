using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0010 IT-01 (endpoints reais do host) — no Subsolo.Api de verdade (sem os dublês de teste), em Development e
/// em Production, todo endpoint mapeado (módulos, <c>/api/v1/antiforgery</c>, health checks, OpenAPI) declara
/// <c>RequireAuthorization(&lt;política&gt;)</c> ou <c>AllowAnonymous()</c>; os health checks e o endpoint de
/// anti-forgery são públicos. A regra com o assembly de exemplo fica em <c>Subsolo.ArchitectureTests/Security</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class HostEndpointsAuthorizationTests(PostgresFixture postgres)
{
    [Theory]
    [Trait("Spec", "SPEC-0010:IT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("Development")]
    [InlineData("Production")]
    public void EveryHostEndpointDeclaresAuthorizationExplicitly(string environment)
    {
        using var factory = new SecurityApiFactory(environment, postgres.ConnectionString, withTestDoubles: false);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName).ToList();

        routes.ShouldContain("/api/v1/cities/{slug}/agenda");
        routes.ShouldContain("/api/v1/antiforgery");
        routes.ShouldContain("/health");

        var undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName)
            .ToList();
        undeclared.ShouldBeEmpty(
            $"endpoints sem RequireAuthorization nem AllowAnonymous ({environment}): {string.Join(", ", undeclared)}");

        foreach (var route in new[] { "/health", "/api/v1/antiforgery" })
        {
            endpoints.Where(endpoint => endpoint.RoutePattern.RawText == route)
                .ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null, $"{route} precisa ser público");
        }
    }
}
