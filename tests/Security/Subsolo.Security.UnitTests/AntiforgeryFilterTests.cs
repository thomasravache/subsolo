using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Security.UnitTests;

/// <summary>
/// SPEC-0010 UT-01 — o filtro de anti-forgery: uma mutação (POST, PUT, PATCH, DELETE) sem o cabeçalho
/// <c>X-CSRF</c> não chega ao endpoint e vira 400 ProblemDetails <c>code=security.csrf_invalid</c>; uma requisição
/// GET passa direto para o endpoint.
/// </summary>
public sealed class AntiforgeryFilterTests
{
    private const string EndpointResult = "resultado-do-endpoint";

    [Fact]
    [Trait("Spec", "SPEC-0010:UT-01")]
    public void HeaderNameIsXCsrf() => AntiforgeryFilter.HeaderName.ShouldBe("X-CSRF");

    [Theory]
    [Trait("Spec", "SPEC-0010:UT-01")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MutationWithoutCsrfHeaderReturnsCsrfInvalidWithoutCallingTheEndpoint(string method)
    {
        var context = CreateContext(method);
        var called = false;

        var result = await new AntiforgeryFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ =>
            {
                called = true;
                return ValueTask.FromResult<object?>(EndpointResult);
            });

        called.ShouldBeFalse("a mutação sem X-CSRF chegou ao endpoint");
        var problem = result.ShouldBeAssignableTo<IResult>();
        problem.ShouldNotBeNull();
        await problem.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldNotBeNull().ShouldStartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        document.RootElement.GetProperty("code").GetString().ShouldBe("security.csrf_invalid");
    }

    [Fact]
    [Trait("Spec", "SPEC-0010:UT-01")]
    public async Task GetRequestPassesToTheEndpoint()
    {
        var context = CreateContext("GET");
        var called = false;

        var result = await new AntiforgeryFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ =>
            {
                called = true;
                return ValueTask.FromResult<object?>(EndpointResult);
            });

        called.ShouldBeTrue("o GET não chegou ao endpoint");
        result.ShouldBe(EndpointResult);
    }

    /// <summary>Requisição em memória com os serviços que o filtro e o ProblemDetails podem usar.</summary>
    private static DefaultHttpContext CreateContext(string method)
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
        return context;
    }
}
