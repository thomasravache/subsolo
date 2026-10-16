using Shouldly;
using Subsolo.ArchitectureTests.Security.Samples;
using Xunit;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0010 IT-01 — teste de arquitetura do ADR-0003: um endpoint sem <c>RequireAuthorization</c> nem
/// <c>AllowAnonymous</c> faz a regra falhar nomeando-o; os endpoints declarados e os endpoints reais de todos os
/// módulos passam. Os endpoints do host (Subsolo.Api) são verificados no IT-01 de
/// <c>tests/Security/Subsolo.Security.IntegrationTests</c>.
/// </summary>
public sealed class EndpointAuthorizationTests
{
    [Fact]
    [Trait("Spec", "SPEC-0010:IT-01")]
    [Trait("Kind", "Integration")]
    public void EndpointWithoutPolicyNorAllowAnonymousFailsNamingIt()
    {
        var endpoints = EndpointAuthorizationRules.EndpointsOf(
            new UndeclaredSampleEndpoint(),
            new AnonymousSampleEndpoint(),
            new ProtectedSampleEndpoint());

        endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ShouldBe(
            [UndeclaredSampleEndpoint.Route, AnonymousSampleEndpoint.Route, ProtectedSampleEndpoint.Route],
            ignoreOrder: true);

        var failure = Should.Throw<Exception>(() => EndpointAuthorizationRules.Check(endpoints));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(UndeclaredSampleEndpoint.Route, Case.Sensitive, failure.Message);
        failure.Message.ShouldNotContain(AnonymousSampleEndpoint.Route, Case.Sensitive, failure.Message);
        failure.Message.ShouldNotContain(ProtectedSampleEndpoint.Route, Case.Sensitive, failure.Message);
    }

    [Fact]
    [Trait("Spec", "SPEC-0010:IT-01")]
    [Trait("Kind", "Integration")]
    public void DeclaredEndpointsPass()
    {
        var endpoints = EndpointAuthorizationRules.EndpointsOf(new AnonymousSampleEndpoint(), new ProtectedSampleEndpoint());

        endpoints.Count.ShouldBe(2);
        Should.NotThrow(() => EndpointAuthorizationRules.Check(endpoints));
    }

    [Fact]
    [Trait("Spec", "SPEC-0010:IT-01")]
    [Trait("Kind", "Integration")]
    public void EveryRealModuleEndpointDeclaresAuthorization()
    {
        var endpoints = EndpointAuthorizationRules.ModuleEndpoints();

        // Não vale passar sem avaliar nada: o endpoint público da agenda (módulo Events) está entre os avaliados.
        endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ShouldContain("/api/v1/cities/{slug}/agenda");
        Should.NotThrow(() => EndpointAuthorizationRules.Check(endpoints));
    }
}
