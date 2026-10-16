using System.Net;
using System.Text.Json;
using Subsolo.Api.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-05 — fora de Development, uma exceção não tratada vira 500 com ProblemDetails (RFC 9457) sem stack
/// trace, sem o tipo/mensagem da exceção e sem nomes de tipos internos.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProblemDetailsOutsideDevelopmentTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0007:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task UnhandledExceptionInProductionReturns500ProblemDetailsWithoutStackTrace()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory("Production", postgres.ConnectionString, withTestEndpoints: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ThrowingEndpoint.Route, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        document.RootElement.TryGetProperty("title", out _).ShouldBeTrue("ProblemDetails sem title");

        body.ShouldNotContain(ThrowingEndpoint.SecretDetail, Case.Insensitive, "a mensagem da exceção vazou");
        body.ShouldNotContain(nameof(InvalidOperationException), Case.Insensitive, "o tipo da exceção vazou");
        body.ShouldNotContain(nameof(ThrowingEndpoint), Case.Insensitive, "o stack trace vazou");
        body.ShouldNotContain("stack", Case.Insensitive, "o stack trace vazou");
        body.ShouldNotContain(" at ", Case.Sensitive, "o stack trace vazou");
    }
}
