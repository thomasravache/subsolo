using System.Net;
using System.Text.Json;
using Subsolo.Common.Presentation;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>Asserções comuns das respostas da API.</summary>
internal static class ProblemAssertions
{
    /// <summary>A resposta é ProblemDetails (RFC 9457) com o status e o código (<c>code</c>) esperados.</summary>
    public static async Task ShouldBeProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(status, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {UsersApiClient.Excerpt(body)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status, body);
        document.RootElement.GetProperty(ApiResults.CodeExtension).GetString().ShouldBe(code, body);
    }

    /// <summary>A resposta tem <c>Cache-Control: no-store</c>.</summary>
    public static void ShouldBeNoStore(this HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        cacheControl.ShouldNotBeNull(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} ({(int)response.StatusCode}) sem Cache-Control");
        cacheControl.NoStore.ShouldBeTrue(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} ({(int)response.StatusCode}): Cache-Control {cacheControl}");
    }
}
