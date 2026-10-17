using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Security.IntegrationTests.Infrastructure;

namespace Subsolo.Security.IntegrationTests;

/// <summary>
/// SPEC-0010 IT-04 — anti-forgery nas mutações: um POST sem o cabeçalho <c>X-CSRF</c> (ou com um token inválido)
/// responde 400 ProblemDetails <c>code=security.csrf_invalid</c>; <c>GET /api/v1/antiforgery</c> responde 200 com
/// <c>{token}</c> e o cookie, e o POST com esse token no <c>X-CSRF</c> (e o cookie) passa com 200.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AntiforgeryTests(PostgresFixture postgres)
{
    private const string AntiforgeryRoute = "/api/v1/antiforgery";
    private const string CsrfHeader = "X-CSRF";

    [Fact]
    [Trait("Spec", "SPEC-0010:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task PostWithoutTokenReturns400CsrfInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(SecurityTestEndpoints.Mutation, content: null, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0010:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task PostWithInvalidTokenReturns400CsrfInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();
        (await GetTokenAsync(client, ct)).ShouldNotBeNullOrWhiteSpace();

        using var request = new HttpRequestMessage(HttpMethod.Post, SecurityTestEndpoints.Mutation);
        request.Headers.Add(CsrfHeader, "token-invalido");
        using var response = await client.SendAsync(request, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0010:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task PostWithTokenFromAntiforgeryEndpointReturns200()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new SecurityApiFactory("Production", postgres.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var token = await GetTokenAsync(client, ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, SecurityTestEndpoints.Mutation);
        request.Headers.Add(CsrfHeader, token);
        using var response = await client.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary><c>GET /api/v1/antiforgery</c> anônimo: 200 com <c>{token}</c> e um cookie.</summary>
    private static async Task<string> GetTokenAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(AntiforgeryRoute, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).ShouldBeTrue("GET /api/v1/antiforgery sem cookie");
        cookies.ShouldNotBeEmpty();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = payload.GetProperty("token").GetString();
        token.ShouldNotBeNullOrWhiteSpace(body);
        return token;
    }
}
