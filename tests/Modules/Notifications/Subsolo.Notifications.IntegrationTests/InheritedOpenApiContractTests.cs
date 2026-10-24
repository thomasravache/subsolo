using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0031 CT-01 (Emenda 1, contrato v2) — o OpenAPI gerado pela API (<c>GET /openapi/v1.json</c>) tem os ajustes
/// herdados do §6 (o snapshot <c>docs/api/openapi.v1.json</c> muda só por eles; a igualdade com o snapshot é o
/// SPEC-0007:CT-01):
/// <list type="bullet">
/// <item><c>PUT /api/v1/me/notification-preferences</c> documenta o 400 <c>notifications.invalid_preferences</c>;</item>
/// <item><c>POST /api/v1/notifications/unsubscribe/one-click?token=…</c> existe, com o corpo
/// <c>application/x-www-form-urlencoded</c>, 204 e 400 <c>notifications.invalid_token</c> (sem o 400 de X-CSRF);</item>
/// <item>cada show de <c>GET /api/v1/shows?profileId=…</c> (os shows do perfil, <c>ProfileShowResponse</c>) tem
/// <c>flyerId</c> anulável.</item>
/// </list>
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class InheritedOpenApiContractTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0031:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task ThePreferencesPutDocumentsInvalidPreferences()
    {
        var document = await OpenApiAsync();

        var put = Operation(document, "/api/v1/me/notification-preferences", "put");

        Description(put, "400").ShouldContain("code=notifications.invalid_preferences", Case.Sensitive);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheOneClickUnsubscribeIsAPublicFormPostWith204And400()
    {
        var document = await OpenApiAsync();

        var post = Operation(document, "/api/v1/notifications/unsubscribe/one-click", "post");

        post.GetProperty("parameters").EnumerateArray()
            .Any(parameter => parameter.GetProperty("name").GetString() == "token" && parameter.GetProperty("in").GetString() == "query")
            .ShouldBeTrue($"sem o parâmetro token na query: {post}");
        post.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/x-www-form-urlencoded", out _)
            .ShouldBeTrue($"o corpo não é application/x-www-form-urlencoded: {post}");
        post.GetProperty("responses").TryGetProperty("204", out _).ShouldBeTrue($"sem o 204: {post}");
        var badRequest = Description(post, "400");
        badRequest.ShouldContain("code=notifications.invalid_token", Case.Sensitive);
        badRequest.ShouldNotContain("security.csrf_invalid", Case.Sensitive, "o um clique não usa X-CSRF (o token assinado é a autorização)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:CT-01")]
    [Trait("Kind", "Integration")]
    public async Task EachProfileShowHasANullableFlyerId()
    {
        var document = await OpenApiAsync();

        var show = document.GetProperty("components").GetProperty("schemas").GetProperty("ProfileShowResponse");

        show.GetProperty("properties").TryGetProperty("flyerId", out var flyerId).ShouldBeTrue($"ProfileShowResponse sem flyerId: {show}");
        var types = flyerId.TryGetProperty("type", out var type)
            ? (type.ValueKind == JsonValueKind.Array ? [.. type.EnumerateArray().Select(item => item.GetString())] : [type.GetString()])
            : new List<string?>();
        types.ShouldBe(["null", "string"], ignoreOrder: true, $"flyerId não é string anulável: {flyerId}");
    }

    private async Task<JsonElement> OpenApiAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        using var client = api.CreateClient();
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }

    private static JsonElement Operation(JsonElement document, string path, string method)
    {
        document.GetProperty("paths").TryGetProperty(path, out var item).ShouldBeTrue($"o OpenAPI não tem {path}");
        item.TryGetProperty(method, out var operation).ShouldBeTrue($"o OpenAPI não tem {method.ToUpperInvariant()} {path}");
        return operation;
    }

    private static string Description(JsonElement operation, string status)
    {
        operation.GetProperty("responses").TryGetProperty(status, out var response).ShouldBeTrue($"sem a resposta {status}: {operation}");
        return response.GetProperty("description").GetString() ?? string.Empty;
    }
}
