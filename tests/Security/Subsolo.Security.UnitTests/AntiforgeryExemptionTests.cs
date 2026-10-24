using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Security.UnitTests;

/// <summary>
/// SPEC-0032 H31-03 (item herdado da revisão da SPEC-0031) — o ramo isento do filtro de anti-forgery: uma mutação (POST,
/// PUT, PATCH, DELETE) sem <c>X-CSRF</c> num endpoint marcado com <see cref="CsrfExemptMetadata"/> chega ao endpoint; a
/// mesma mutação num endpoint sem a marca continua recusada (400 <c>security.csrf_invalid</c>, o SPEC-0010:UT-01). E
/// <c>.ExemptFromCsrf(motivo)</c> exige um motivo.
/// </summary>
public sealed class AntiforgeryExemptionTests
{
    private const string EndpointResult = "resultado-do-endpoint";

    [Theory]
    [Trait("Spec", "SPEC-0032:H31-03")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task AMutationWithoutTheHeaderReachesAnExemptEndpoint(string method)
    {
        var context = CreateContext(method, new CsrfExemptMetadata("RFC 8058: o token assinado da query autoriza"));

        var (called, result) = await InvokeAsync(context);

        called.ShouldBeTrue("a mutação isenta não chegou ao endpoint");
        result.ShouldBe(EndpointResult);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-03")]
    public async Task AMutationWithoutTheHeaderIsRejectedOnAnEndpointWithoutTheExemption()
    {
        var context = CreateContext("POST");

        var (called, result) = await InvokeAsync(context);

        called.ShouldBeFalse("a mutação sem X-CSRF chegou a um endpoint que não é isento");
        await result.ShouldBeAssignableTo<IResult>().ShouldNotBeNull().ExecuteAsync(context);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0032:H31-03")]
    [InlineData("")]
    [InlineData("   ")]
    public void TheExemptionRequiresAReason(string reason)
    {
        Should.Throw<ArgumentException>(() => new TestConventionBuilder().ExemptFromCsrf(reason));
    }

    private static async Task<(bool Called, object? Result)> InvokeAsync(DefaultHttpContext context)
    {
        var called = false;
        var result = await new AntiforgeryFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ =>
            {
                called = true;
                return ValueTask.FromResult<object?>(EndpointResult);
            });
        return (called, result);
    }

    /// <summary>Requisição em memória (como no SPEC-0010:UT-01) roteada a um endpoint com os metadados dados.</summary>
    private static DefaultHttpContext CreateContext(string method, params object[] metadata)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .AddAntiforgery()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/api/v1/__tests/mutation";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "endpoint de teste"));
        return context;
    }

    private sealed class TestConventionBuilder : Microsoft.AspNetCore.Builder.IEndpointConventionBuilder
    {
        public void Add(Action<Microsoft.AspNetCore.Builder.EndpointBuilder> convention)
        {
        }
    }
}
