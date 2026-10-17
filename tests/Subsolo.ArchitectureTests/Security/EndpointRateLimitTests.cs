using Shouldly;
using Subsolo.ArchitectureTests.Security.Samples;
using Xunit;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0014 IT-10 — teste de arquitetura do rate limit (ao lado do <see cref="EndpointAuthorizationRules"/>): um
/// endpoint sem política de rate limit (ou com o rate limit desligado) faz a regra falhar nomeando-o; o endpoint com
/// política e as rotas dispensadas (<c>/health</c>, <c>/alive</c>, <c>/openapi/*</c> — Emenda v2) passam; todo endpoint real dos módulos declara uma política, e <c>/api/v1/me</c>,
/// <c>/api/v1/auth/logout</c>, <c>/api/v1/cities</c> e a agenda da cidade usam <c>default</c>. Os endpoints do host
/// (token anti-forgery, health) são verificados em <c>tests/Security/Subsolo.Security.IntegrationTests</c>.
/// </summary>
public sealed class EndpointRateLimitTests
{
    [Fact]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    public void EndpointWithoutRateLimitPolicyFailsNamingIt()
    {
        var endpoints = EndpointAuthorizationRules.EndpointsOf(
            new UnlimitedSampleEndpoint(),
            new DisabledRateLimitSampleEndpoint(),
            new LimitedSampleEndpoint(),
            new HealthSampleEndpoint(),
            new AliveSampleEndpoint(),
            new OpenApiSampleEndpoint(),
            new HealthLookalikeSampleEndpoint());

        endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ShouldBe(
            [
                UnlimitedSampleEndpoint.Route, DisabledRateLimitSampleEndpoint.Route, LimitedSampleEndpoint.Route,
                HealthSampleEndpoint.Route, AliveSampleEndpoint.Route, OpenApiSampleEndpoint.Route,
                HealthLookalikeSampleEndpoint.Route,
            ],
            ignoreOrder: true);

        var failure = Should.Throw<Exception>(() => EndpointRateLimitRules.Check(endpoints));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldStartWith(EndpointRateLimitRules.Rule, Case.Sensitive, failure.Message);
        var named = failure.Message[EndpointRateLimitRules.Rule.Length..];
        named.ShouldContain(UnlimitedSampleEndpoint.Route, Case.Sensitive, failure.Message);
        named.ShouldContain(DisabledRateLimitSampleEndpoint.Route, Case.Sensitive, failure.Message);
        named.ShouldContain(HealthLookalikeSampleEndpoint.Route, Case.Sensitive, failure.Message);
        named.ShouldNotContain(LimitedSampleEndpoint.Route, Case.Sensitive, failure.Message);
        named.ShouldNotMatch($@"GET {HealthSampleEndpoint.Route}(;|$)", failure.Message);
        named.ShouldNotContain(AliveSampleEndpoint.Route, Case.Sensitive, failure.Message);
        named.ShouldNotContain("/openapi/", Case.Sensitive, failure.Message);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    public void EndpointsWithPolicyAndExemptRoutesPass()
    {
        var endpoints = EndpointAuthorizationRules.EndpointsOf(
            new LimitedSampleEndpoint(), new HealthSampleEndpoint(), new AliveSampleEndpoint(), new OpenApiSampleEndpoint());

        endpoints.Count.ShouldBe(4);
        Should.NotThrow(() => EndpointRateLimitRules.Check(endpoints));
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    public void EveryRealModuleEndpointDeclaresARateLimitPolicy()
    {
        var endpoints = EndpointAuthorizationRules.ModuleEndpoints();

        // Não vale passar sem avaliar nada: os endpoints dos módulos Events e Users estão entre os avaliados.
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ToList();
        routes.ShouldContain("/api/v1/cities");
        routes.ShouldContain("/api/v1/cities/{slug}/agenda");
        routes.ShouldContain("/api/v1/me");
        routes.ShouldContain("/api/v1/auth/logout");
        Should.NotThrow(() => EndpointRateLimitRules.Check(endpoints));
    }

    [Theory]
    [Trait("Spec", "SPEC-0014:IT-10")]
    [Trait("Kind", "Integration")]
    [InlineData("GET", "/api/v1/me")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("GET", "/api/v1/cities")]
    [InlineData("GET", "/api/v1/cities/{slug}/agenda")]
    public void EndpointUsesTheDefaultPolicy(string method, string route)
    {
        var endpoint = EndpointAuthorizationRules.ModuleEndpoints()
            .Where(candidate => candidate.RoutePattern.RawText == route
                && (candidate.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains(method) ?? false))
            .ShouldHaveSingleItem($"{method} {route} não está entre os endpoints dos módulos");

        EndpointRateLimitRules.PolicyOf(endpoint).ShouldBe("default", $"{method} {route}");
    }
}
