using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Time.Testing;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0020 — o Subsolo.Api em memória (<see cref="UsersApiFactory"/>, com o Mailpit do <see cref="MagicLinkHost"/>)
/// ligado ao provedor OIDC falso (<see cref="FakeGoogleProvider"/>) e o fluxo do navegador passo a passo: início
/// (<c>GET /api/v1/auth/google</c>) → authorize do provedor (que emite o código na hora) → retorno ao
/// <c>CallbackPath</c> da API, por GET (<c>response_mode=query</c>) ou POST de formulário (<c>form_post</c>), conforme o
/// provedor responder.
/// </summary>
internal static partial class GoogleHost
{
    public const string ProvidersRoute = "/api/v1/auth/providers";

    public const string StartRoute = "/api/v1/auth/google";

    public const string CallbackRoute = "/api/v1/auth/google/callback";

    public const string ClientId = "subsolo-it";

    /// <summary>Segredo de cliente só deste teste (o provedor falso aceita qualquer um).</summary>
    public const string ClientSecret = "segredo-do-cliente-de-teste-it";

    public const string ExternalCookieName = "__Host-subsolo-ext";

    public const string ProviderErrorRedirect = "/entrar?erro=google";

    public const string EmailNotVerifiedRedirect = "/entrar?erro=google_email";

    /// <summary>Configuração do Google apontando para o emissor <paramref name="issuer"/> do provedor falso.</summary>
    public static Dictionary<string, string> GoogleSettings(FakeGoogleProvider provider, string issuer)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return GoogleSettings(provider.Authority(issuer));
    }

    /// <summary>Configuração do Google com a Authority <paramref name="authority"/>.</summary>
    public static Dictionary<string, string> GoogleSettings(string authority) => new(StringComparer.Ordinal)
    {
        ["Users:Google:Authority"] = authority,
        ["Users:Google:ClientId"] = ClientId,
        ["Users:Google:ClientSecret"] = ClientSecret,
    };

    /// <summary>A API com um database novo, o Mailpit e a configuração <paramref name="overrides"/> (ex.: <see cref="GoogleSettings(FakeGoogleProvider, string)"/>).</summary>
    public static Task<(UsersApiFactory Api, string Database)> StartAsync(
        PostgresFixture postgres,
        MailpitFixture mailpit,
        IReadOnlyDictionary<string, string>? overrides,
        CancellationToken ct,
        FakeTimeProvider? clock = null) =>
        MagicLinkHost.StartAsync(postgres, mailpit, ct, clock, overrides);

    /// <summary>Junta dicionários de configuração (os da direita ganham).</summary>
    public static Dictionary<string, string> Merge(params IReadOnlyDictionary<string, string>[] parts)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            foreach (var (key, value) in part)
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    /// <summary><c>GET /api/v1/auth/google?returnTo=</c> (sem seguir o redirecionamento).</summary>
    public static Task<HttpResponseMessage> StartGoogleAsync(this UsersApiClient client, string? returnTo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        var route = returnTo is null ? StartRoute : QueryHelpers.AddQueryString(StartRoute, "returnTo", returnTo);
        return client.GetAsync(route, ct);
    }

    /// <summary>
    /// Inicia o login, exige o 302 para o authorize do provedor (<c>response_type=code</c>, PKCE S256, <c>state</c>,
    /// <c>nonce</c>, o <see cref="ClientId"/> e o <c>redirect_uri</c> no <see cref="CallbackRoute"/>) e devolve a URL
    /// do authorize.
    /// </summary>
    public static async Task<Uri> StartAndExpectAuthorizeAsync(
        this UsersApiClient client,
        FakeGoogleProvider provider,
        string issuer,
        string? returnTo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        using var start = await client.StartGoogleAsync(returnTo, ct);
        var body = await start.Content.ReadAsStringAsync(ct);
        start.StatusCode.ShouldBe(HttpStatusCode.Redirect, $"GET {StartRoute}: {UsersApiClient.Excerpt(body)}");
        var authorize = start.Headers.Location.ShouldNotBeNull($"GET {StartRoute} sem Location");
        authorize.IsAbsoluteUri.ShouldBeTrue($"Location {authorize}");
        authorize.GetLeftPart(UriPartial.Path).ShouldBe(
            $"{provider.Authority(issuer)}/authorize", $"o início leva ao authorize do provedor: {authorize}");

        var query = QueryHelpers.ParseQuery(authorize.Query);
        Single(query, "response_type").ShouldBe("code", authorize.ToString());
        Single(query, "client_id").ShouldBe(ClientId, authorize.ToString());
        Single(query, "code_challenge_method").ShouldBe("S256", "PKCE com S256");
        Single(query, "code_challenge").ShouldNotBeNullOrWhiteSpace("PKCE: code_challenge");
        Single(query, "state").ShouldNotBeNullOrWhiteSpace("state");
        Single(query, "nonce").ShouldNotBeNullOrWhiteSpace("nonce");
        Single(query, "scope").Split(' ').ShouldContain("openid");
        Single(query, "scope").Split(' ').ShouldContain("email");
        new Uri(Single(query, "redirect_uri")).AbsolutePath.ShouldBe(CallbackRoute, "redirect_uri");
        return authorize;
    }

    /// <summary>
    /// O authorize no provedor (como o navegador, sem cookies da API): devolve o retorno que o provedor manda ao
    /// <c>redirect_uri</c> — a rota e os parâmetros (<c>code</c>, <c>state</c>, …) e se é por POST de formulário.
    /// </summary>
    public static async Task<ProviderResponse> AuthorizeAtProviderAsync(Uri authorize, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(authorize, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
        {
            var location = response.Headers.Location.ShouldNotBeNull("authorize do provedor sem Location");
            var parameters = QueryHelpers.ParseQuery(location.Query)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
            return new ProviderResponse(location.AbsolutePath, parameters, FormPost: false);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"authorize do provedor: {UsersApiClient.Excerpt(body)}");
        var form = FormAction().Match(body);
        form.Success.ShouldBeTrue($"o provedor não devolveu redirecionamento nem formulário: {UsersApiClient.Excerpt(body)}");
        var fields = HiddenInput().Matches(body)
            .ToDictionary(match => match.Groups["name"].Value, match => WebUtility.HtmlDecode(match.Groups["value"].Value), StringComparer.Ordinal);
        return new ProviderResponse(new Uri(WebUtility.HtmlDecode(form.Groups["action"].Value)).AbsolutePath, fields, FormPost: true);
    }

    /// <summary>Entrega ao <c>CallbackPath</c> da API o retorno do provedor (do jeito que ele mandou), com os cookies do cliente.</summary>
    public static Task<HttpResponseMessage> DeliverCallbackAsync(this UsersApiClient client, ProviderResponse callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(callback);
        return callback.FormPost
            ? client.SendAsync(HttpMethod.Post, callback.Path, ct, new FormUrlEncodedContent(callback.Parameters))
            : client.GetAsync(
                QueryHelpers.AddQueryString(
                    callback.Path,
                    callback.Parameters.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))),
                ct);
    }

    /// <summary>
    /// O fluxo inteiro: início → authorize → retorno (com <paramref name="tamper"/> opcional nos parâmetros do retorno);
    /// devolve a resposta do retorno, o que o provedor mandou e quanto o retorno levou na API.
    /// </summary>
    public static async Task<GoogleFlow> SignInWithGoogleAsync(
        this UsersApiClient client,
        FakeGoogleProvider provider,
        string issuer,
        string? returnTo,
        CancellationToken ct,
        Func<ProviderResponse, ProviderResponse>? tamper = null)
    {
        var authorize = await client.StartAndExpectAuthorizeAsync(provider, issuer, returnTo, ct);
        var fromProvider = await AuthorizeAtProviderAsync(authorize, ct);
        fromProvider.Path.ShouldBe(CallbackRoute, "o provedor volta ao CallbackPath da API");
        var delivered = tamper?.Invoke(fromProvider) ?? fromProvider;
        var watch = Stopwatch.StartNew();
        var response = await client.DeliverCallbackAsync(delivered, ct);
        watch.Stop();
        return new GoogleFlow(authorize, fromProvider, response, watch.Elapsed);
    }

    /// <summary>O caminho e a query do <c>Location</c> (relativo ou absoluto no mesmo site), para comparar com <c>/entrar?erro=…</c>.</summary>
    public static string RedirectTarget(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode.ShouldBe(
            HttpStatusCode.Redirect,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: esperado 302");
        var location = response.Headers.Location.ShouldNotBeNull("302 sem Location");
        if (!location.IsAbsoluteUri)
        {
            return location.OriginalString;
        }

        location.Host.ShouldBe("localhost", $"redirecionamento para fora do site: {location}");
        return location.PathAndQuery;
    }

    /// <summary>Os <c>Set-Cookie</c> da resposta com o nome <paramref name="name"/>.</summary>
    public static IReadOnlyList<string> SetCookies(this HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? [.. values.Where(value => value.StartsWith(name + "=", StringComparison.Ordinal))]
            : [];
    }

    private static string Single(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
    {
        query.TryGetValue(key, out var value).ShouldBeTrue($"authorize sem o parâmetro {key}");
        value.Count.ShouldBe(1, $"authorize com {key} repetido");
        return value.ToString();
    }

    [GeneratedRegex("<form[^>]*action=\"(?<action>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormAction();

    [GeneratedRegex("<input[^>]*type=\"hidden\"[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HiddenInput();
}

/// <summary>SPEC-0020 — o que o provedor mandou ao <c>redirect_uri</c>: rota, parâmetros e se é POST de formulário.</summary>
internal sealed record ProviderResponse(string Path, IReadOnlyDictionary<string, string> Parameters, bool FormPost)
{
    /// <summary>O mesmo retorno com <paramref name="name"/> trocado por <paramref name="value"/> (nulo = sem o parâmetro).</summary>
    public ProviderResponse With(string name, string? value)
    {
        var parameters = new Dictionary<string, string>(Parameters, StringComparer.Ordinal);
        if (value is null)
        {
            parameters.Remove(name);
        }
        else
        {
            parameters[name] = value;
        }

        return this with { Parameters = parameters };
    }
}

/// <summary>SPEC-0020 — um fluxo completo: o authorize, o retorno do provedor, a resposta da API ao retorno e a duração dele.</summary>
internal sealed record GoogleFlow(Uri Authorize, ProviderResponse FromProvider, HttpResponseMessage Callback, TimeSpan CallbackDuration);
