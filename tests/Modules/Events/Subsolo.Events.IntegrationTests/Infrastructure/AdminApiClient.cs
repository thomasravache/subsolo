using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Subsolo.Common.Presentation;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// Cliente da API com pote de cookies próprio, o token anti-forgery da SPEC-0010 (<see cref="CsrfHeader"/>) e a entrada
/// de teste (<see cref="AdminTestEndpoints.SignIn"/>); copiado do <c>UsersApiClient</c> da SPEC-0014.
/// </summary>
internal sealed class AdminApiClient(HttpClient http) : IDisposable
{
    public const string SessionCookieName = "__Host-subsolo";

    public const string CsrfHeader = "X-CSRF";

    private const string AntiforgeryRoute = "/api/v1/antiforgery";

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>Envia com os cookies do pote e, se informado, o <c>X-CSRF</c>; os <c>Set-Cookie</c> atualizam o pote.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        CancellationToken cancellationToken,
        object? body = null,
        string? csrfToken = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        if (csrfToken is not null)
        {
            request.Headers.Add(CsrfHeader, csrfToken);
        }

        var response = await http.SendAsync(request, cancellationToken);
        Store(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, route, cancellationToken);

    /// <summary>Mutação com um token anti-forgery novo (pedido com os cookies atuais).</summary>
    public async Task<HttpResponseMessage> MutateAsync(
        HttpMethod method,
        string route,
        object? body,
        CancellationToken cancellationToken) =>
        await SendAsync(method, route, cancellationToken, body, await CsrfTokenAsync(cancellationToken));

    /// <summary><c>GET /api/v1/antiforgery</c> com os cookies atuais: o token para o <c>X-CSRF</c>.</summary>
    public async Task<string> CsrfTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await GetAsync(AntiforgeryRoute, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {AntiforgeryRoute}: {body}");
        using var document = JsonDocument.Parse(body);
        var token = document.RootElement.GetProperty("token").GetString();
        token.ShouldNotBeNullOrWhiteSpace(body);
        return token;
    }

    /// <summary>Entra com <paramref name="email"/> pelo <c>ISignInService</c> do host e exige o cookie de sessão.</summary>
    public async Task SignInAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await MutateAsync(
            HttpMethod.Post,
            AdminTestEndpoints.SignIn,
            new AdminTestEndpoints.SignInRequest(email),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"entrada de teste falhou: {Excerpt(body)}");
        _cookies.ContainsKey(SessionCookieName).ShouldBeTrue($"a entrada não emitiu o cookie {SessionCookieName}");
    }

    public void Dispose() => http.Dispose();

    /// <summary>Começo do corpo (a página de exceção de Development é longa).</summary>
    public static string Excerpt(string body) => body.Length <= 600 ? body : body[..600];

    private void Store(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var cookie in SetCookieHeaderValue.ParseList([.. values]))
        {
            var name = cookie.Name.Value!;
            var expired = cookie.Expires is { } expires && expires <= DateTimeOffset.UtcNow
                || cookie.MaxAge is { } maxAge && maxAge <= TimeSpan.Zero;
            if (expired || cookie.Value.Length == 0)
            {
                _cookies.Remove(name);
            }
            else
            {
                _cookies[name] = cookie.Value.Value!;
            }
        }
    }
}

/// <summary>Asserções das respostas da API.</summary>
internal static class ResponseAssertions
{
    /// <summary>ProblemDetails (RFC 9457) com o status e o código (<c>code</c>) esperados; devolve o corpo.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var where = $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}";
        response.StatusCode.ShouldBe(status, $"{where}: {AdminApiClient.Excerpt(body)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", $"{where}: {body}");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty(ApiResults.CodeExtension).GetString().ShouldBe(code, $"{where}: {body}");
        return document.RootElement.Clone();
    }

    /// <summary>O ProblemDetails de validação cita o campo <paramref name="field"/> em <c>errors</c> (sem diferenciar caixa).</summary>
    public static void ShouldHaveFieldError(this JsonElement problem, string field)
    {
        problem.TryGetProperty("errors", out var errors).ShouldBeTrue($"ProblemDetails sem errors: {problem}");
        errors.EnumerateObject()
            .Select(property => property.Name)
            .ShouldContain(name => string.Equals(name, field, StringComparison.OrdinalIgnoreCase), $"errors sem o campo {field}: {errors}");
    }

    /// <summary>200/201 com JSON; devolve o corpo.</summary>
    public static async Task<JsonElement> ShouldBeJsonAsync(this HttpResponseMessage response, HttpStatusCode status, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var where = $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}";
        response.StatusCode.ShouldBe(status, $"{where}: {AdminApiClient.Excerpt(body)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", $"{where}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
