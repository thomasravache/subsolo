using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.IntegrationEvents;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0022 — cliente da API com pote de cookies próprio, o token anti-forgery da SPEC-0010 (<see cref="CsrfHeader"/>),
/// a entrada de teste (<see cref="FlyersTestEndpoints.SignIn"/>) e o envio de flyer em <c>multipart/form-data</c>
/// (copiado do <see cref="AdminApiClient"/> da SPEC-0017).
/// </summary>
internal sealed class FlyersApiClient(HttpClient http) : IDisposable
{
    public const string FlyersRoute = "/api/v1/flyers";

    public const string SessionCookieName = "__Host-subsolo";

    public const string CsrfHeader = "X-CSRF";

    private const string AntiforgeryRoute = "/api/v1/antiforgery";

    private const string MeRoute = "/api/v1/me";

    /// <summary>Prazo para a outbox e a inbox do Users (a cada 2 s) entregarem o papel <c>artist</c>.</summary>
    private static readonly TimeSpan RoleDeliveryTimeout = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>
    /// <c>POST /api/v1/flyers</c> com <paramref name="content"/> no campo <paramref name="field"/> (o padrão do contrato é
    /// <c>file</c>), nome e tipo declarados pelo cliente, e o <c>X-CSRF</c> (novo, a menos que <paramref name="csrfToken"/>
    /// seja informado) se <paramref name="withCsrf"/>.
    /// </summary>
    public async Task<HttpResponseMessage> UploadAsync(
        byte[] content,
        CancellationToken ct,
        string fileName = "flyer.jpg",
        string contentType = "image/jpeg",
        string field = "file",
        bool withCsrf = true,
        string? csrfToken = null)
    {
        var token = withCsrf ? csrfToken ?? await CsrfTokenAsync(ct) : null;
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, field, fileName);
        return await SendAsync(HttpMethod.Post, FlyersRoute, ct, form, token);
    }

    /// <summary>Envia com os cookies do pote e, se informado, o <c>X-CSRF</c>; os <c>Set-Cookie</c> atualizam o pote.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        CancellationToken cancellationToken,
        HttpContent? content = null,
        string? csrfToken = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative)) { Content = content };
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

    /// <summary>Entra com <paramref name="email"/> pelo <c>ISignInService</c> do host, exige o cookie de sessão e devolve o id.</summary>
    public async Task<Guid> SignInAsync(string email, CancellationToken cancellationToken)
    {
        using var body = JsonContent.Create(new FlyersTestEndpoints.SignInRequest(email));
        using var response = await SendAsync(
            HttpMethod.Post, FlyersTestEndpoints.SignIn, cancellationToken, body, await CsrfTokenAsync(cancellationToken));
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"entrada de teste falhou: {Excerpt(text)}");
        _cookies.ContainsKey(SessionCookieName).ShouldBeTrue($"a entrada não emitiu o cookie {SessionCookieName}");
        using var document = JsonDocument.Parse(text);
        return document.RootElement.GetProperty("userId").GetGuid();
    }

    /// <summary>Os papéis de <c>GET /api/v1/me</c>.</summary>
    public async Task<IReadOnlyList<string>> RolesAsync(CancellationToken ct)
    {
        using var response = await GetAsync(MeRoute, ct);
        var me = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return [.. me.GetProperty("roles").EnumerateArray().Select(role => role.GetString() ?? string.Empty)];
    }

    /// <summary>
    /// Concede o papel <c>artist</c> a <paramref name="userId"/> como a aprovação de perfil da SPEC-0021 faz (o evento
    /// <see cref="ArtistRoleGrantedIntegrationEvent"/> do contrato do Users) e espera <c>/me</c> mostrar o papel.
    /// </summary>
    public async Task BecomeArtistAsync(IServiceProvider services, Guid userId, CancellationToken ct)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(
                new ArtistRoleGrantedIntegrationEvent(userId, Guid.CreateVersion7())
                {
                    Id = Guid.CreateVersion7(),
                    OccurredOnUtc = DateTime.UtcNow,
                },
                ct);
        }

        (await Timing.EventuallyAsync(async () => (await RolesAsync(ct)).Contains("artist"), RoleDeliveryTimeout, ct))
            .ShouldBeTrue("o papel artist não chegou ao usuário de teste");
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

/// <summary>SPEC-0022 — resposta de <c>POST /api/v1/flyers</c> (§6).</summary>
internal sealed record FlyerUploaded(string FlyerId, string ThumbUrl, string FullUrl, string OgUrl)
{
    /// <summary>Lê o corpo <c>{flyerId, thumbUrl, fullUrl, ogUrl}</c> com o status esperado.</summary>
    public static async Task<FlyerUploaded> ReadAsync(HttpResponseMessage response, HttpStatusCode status, CancellationToken ct)
    {
        var body = await response.ShouldBeJsonAsync(status, ct);
        string Text(string name)
        {
            body.TryGetProperty(name, out var value).ShouldBeTrue($"resposta sem {name}: {body}");
            return value.GetString().ShouldNotBeNull($"{name} nulo: {body}");
        }

        return new FlyerUploaded(Text("flyerId"), Text("thumbUrl"), Text("fullUrl"), Text("ogUrl"));
    }

    /// <summary>As três variantes: (variante, URL pública, caminho relativo ao <c>Media:Root</c>).</summary>
    public IReadOnlyList<(string Variant, string Url, string RelativePath)> Variants =>
    [
        ("thumb", ThumbUrl, Path.Combine("flyers", $"{FlyerId}-thumb.webp")),
        ("full", FullUrl, Path.Combine("flyers", $"{FlyerId}-full.webp")),
        ("og", OgUrl, Path.Combine("flyers", $"{FlyerId}-og.jpg")),
    ];
}

/// <summary>Medidas de tempo e espera dos testes.</summary>
internal static class Timing
{
    /// <summary>Percentil pelo método do posto mais próximo (nearest-rank).</summary>
    public static TimeSpan Percentile(IReadOnlyCollection<TimeSpan> values, int percent)
    {
        var sorted = values.Order().ToList();
        var rank = (int)Math.Ceiling(percent / 100.0 * sorted.Count);
        return sorted[Math.Max(0, rank - 1)];
    }

    /// <summary>Espera <paramref name="condition"/> ficar verdadeira (consultando a cada 200 ms) até <paramref name="timeout"/>.</summary>
    public static async Task<bool> EventuallyAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            if (await condition())
            {
                return true;
            }

            if (watch.Elapsed >= timeout)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        }
    }
}
