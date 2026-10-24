using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Presentation.Security;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0032 H31-03 (item herdado da revisão da SPEC-0031; contrato v2) — no Subsolo.Api de verdade (sem os dublês de
/// teste), em Development e em Production, os endpoints com <see cref="CsrfExemptMetadata"/> (dispensam o <c>X-CSRF</c>)
/// são exatamente a lista permitida { <c>UnsubscribeNoticeEmailOneClick</c> }, e todos são públicos
/// (<c>AllowAnonymous</c>: a credencial é da própria requisição, nunca o cookie de sessão) e dizem o motivo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CsrfExemptEndpointsTests(PostgresFixture postgres)
{
    private static readonly string[] Allowed = ["UnsubscribeNoticeEmailOneClick"];

    [Theory]
    [Trait("Spec", "SPEC-0032:H31-03")]
    [Trait("Kind", "Integration")]
    [InlineData("Development")]
    [InlineData("Production")]
    public void OnlyTheAllowedEndpointsAreExemptFromCsrfAndAllArePublic(string environment)
    {
        using var factory = new SecurityApiFactory(environment, postgres.ConnectionString, withTestDoubles: false);
        var exempt = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<CsrfExemptMetadata>() is not null)
            .ToList();

        exempt.Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? endpoint.DisplayName)
            .ShouldBe(Allowed, ignoreOrder: true, $"endpoints isentos de CSRF fora da lista permitida ({environment})");
        exempt.ShouldAllBe(
            endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null,
            $"endpoint isento de CSRF que não é AllowAnonymous ({environment})");
        exempt.ShouldAllBe(
            endpoint => !string.IsNullOrWhiteSpace(endpoint.Metadata.GetMetadata<CsrfExemptMetadata>()!.Reason),
            "endpoint isento de CSRF sem motivo");
    }
}
