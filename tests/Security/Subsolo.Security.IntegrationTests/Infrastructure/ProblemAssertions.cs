using System.Net;
using System.Text.Json;
using Subsolo.Common.Presentation;

namespace Subsolo.Security.IntegrationTests.Infrastructure;

/// <summary>Asserção comum: a resposta é ProblemDetails (RFC 9457) com o status e o código (<c>code</c>) esperados.</summary>
internal static class ProblemAssertions
{
    public static async Task ShouldBeProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(status, $"{response.RequestMessage?.RequestUri}: {body}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status, body);
        document.RootElement.GetProperty(ApiResults.CodeExtension).GetString().ShouldBe(code, body);
    }
}
