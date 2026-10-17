using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// Cliente da API com um pote de cookies próprio (o teste vê e reaproveita o valor exato do cookie de sessão
/// <see cref="SessionCookieName"/>, inclusive depois de o servidor mandar apagá-lo), o token anti-forgery da SPEC-0010
/// e a entrada de teste pelo <c>ISignInService</c>.
/// </summary>
internal sealed class UsersApiClient(HttpClient http) : IDisposable
{
    public const string SessionCookieName = "__Host-subsolo";

    public const string CsrfHeader = "X-CSRF";

    private const string AntiforgeryRoute = "/api/v1/antiforgery";

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>IP de origem das próximas requisições (<see cref="UsersApiFactory.RemoteIpHeader"/>); nulo = IP padrão.</summary>
    public string? RemoteIp { get; set; }

    /// <summary>Valor atual do cookie de sessão no pote, ou nulo.</summary>
    public string? SessionCookie => _cookies.GetValueOrDefault(SessionCookieName);

    /// <summary>
    /// Envia a requisição com os cookies do pote (o de sessão trocado por <paramref name="sessionCookie"/>, se informado)
    /// e o <c>X-CSRF</c> <paramref name="csrfToken"/>; os <c>Set-Cookie</c> da resposta atualizam o pote.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        CancellationToken cancellationToken,
        HttpContent? content = null,
        string? csrfToken = null,
        string? sessionCookie = null)
    {
        using var request = new HttpRequestMessage(method, route) { Content = content };
        var cookies = new Dictionary<string, string>(_cookies, StringComparer.Ordinal);
        if (sessionCookie is not null)
        {
            cookies[SessionCookieName] = sessionCookie;
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        if (csrfToken is not null)
        {
            request.Headers.Add(CsrfHeader, csrfToken);
        }

        if (RemoteIp is not null)
        {
            request.Headers.Add(UsersApiFactory.RemoteIpHeader, RemoteIp);
        }

        var response = await http.SendAsync(request, cancellationToken);
        Store(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken, string? sessionCookie = null) =>
        SendAsync(HttpMethod.Get, route, cancellationToken, sessionCookie: sessionCookie);

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

    /// <summary>
    /// Entra com <paramref name="email"/> pelo <c>ISignInService.SignInAsync</c> do host (endpoint de teste) e exige
    /// 200 e o cookie de sessão emitido.
    /// </summary>
    public async Task<SignedIn> SignInAsync(string email, CancellationToken cancellationToken)
    {
        var token = await CsrfTokenAsync(cancellationToken);
        using var response = await SendAsync(
            HttpMethod.Post,
            UsersTestEndpoints.SignIn,
            cancellationToken,
            JsonContent.Create(new UsersTestEndpoints.SignInRequest(email)),
            token);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"entrada pelo ISignInService.SignInAsync falhou: {Excerpt(body)}");
        SessionCookie.ShouldNotBeNullOrWhiteSpace($"SignInAsync não emitiu o cookie {SessionCookieName}");
        using var document = JsonDocument.Parse(body);
        return new SignedIn(
            document.RootElement.GetProperty("userId").GetGuid(),
            document.RootElement.GetProperty("isNewUser").GetBoolean(),
            SessionCookie);
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

/// <summary>Resultado da entrada de teste: o usuário, se é novo, e o valor do cookie de sessão emitido.</summary>
internal sealed record SignedIn(Guid UserId, bool IsNewUser, string SessionCookie);
