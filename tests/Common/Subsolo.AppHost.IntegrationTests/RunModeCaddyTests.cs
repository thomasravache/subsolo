using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0040 UT-01 — no modelo do <c>Subsolo.AppHost</c> em modo run (montado pelo
/// <see cref="DistributedApplicationTestingBuilder"/>, sem subir os recursos), o mesmo Caddy do Compose fica na frente
/// da aplicação: recurso <c>caddy</c> com o <c>infra/caddy/Caddyfile</c> do repositório montado só leitura em
/// <c>/etc/caddy/Caddyfile</c>, endpoint <c>https</c> fixo em 8443 sem proxy do Aspire, <c>API_UPSTREAM</c> e
/// <c>WEB_UPSTREAM</c> referenciando os endpoints <c>http</c> da <c>api</c> e do <c>web</c>; e o endereço público
/// (<c>App__PublicUrl</c> da <c>api</c> e <c>SUBSOLO_PUBLIC_URL</c> do <c>web</c>) é <c>https://localhost:8443</c>.
/// </summary>
public sealed class RunModeCaddyModelTests
{
    private const string PublicUrl = "https://localhost:8443";
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0040:UT-01")]
    [Trait("Kind", "Integration")]
    public async Task RunModeHasTheCaddyInFrontOfApiAndWebOnHttps8443()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);
        var executionContext = app.Services.GetRequiredService<DistributedApplicationExecutionContext>();
        var resources = app.Services.GetRequiredService<DistributedApplicationModel>().Resources;

        var caddy = resources.SingleOrDefault(resource => resource.Name == "caddy");
        caddy.ShouldNotBeNull(
            $"o AppHost não tem o recurso caddy no modo run (recursos: {string.Join(", ", resources.Select(r => r.Name))})");

        // O Caddyfile do repositório, montado só leitura onde a imagem oficial o lê.
        var caddyfile = Path.Combine(RepositoryRoot(), "infra", "caddy", "Caddyfile");
        var mounts = caddy.Annotations.OfType<ContainerMountAnnotation>().ToList();
        var mount = mounts.SingleOrDefault(candidate => candidate.Target == "/etc/caddy/Caddyfile");
        mount.ShouldNotBeNull(
            $"o caddy não monta /etc/caddy/Caddyfile (montagens: {string.Join(", ", mounts.Select(m => $"{m.Source}→{m.Target}"))})");
        mount.Type.ShouldBe(ContainerMountType.BindMount);
        Path.GetFullPath(mount.Source!).ShouldBe(Path.GetFullPath(caddyfile));
        mount.IsReadOnly.ShouldBeTrue("o Caddyfile deve ser montado só leitura");

        // Emenda 1 — /media/* sai do Caddy: o Media__Root do run (artifacts/media) montado só leitura em /srv/media.
        var media = mounts.SingleOrDefault(candidate => candidate.Target == "/srv/media");
        media.ShouldNotBeNull(
            $"o caddy não monta /srv/media (montagens: {string.Join(", ", mounts.Select(m => $"{m.Source}→{m.Target}"))})");
        media.Type.ShouldBe(ContainerMountType.BindMount);
        Path.GetFullPath(media.Source!).TrimEnd(Path.DirectorySeparatorChar)
            .ShouldBe(Path.GetFullPath(Path.Combine(RepositoryRoot(), "artifacts", "media")));
        media.IsReadOnly.ShouldBeTrue("artifacts/media deve ser montado só leitura no caddy");

        // Emenda 1 — a CA interna do Caddy persiste no volume subsolo-caddy-data (o aviso de certificado aparece uma vez).
        var data = mounts.SingleOrDefault(candidate => candidate.Target == "/data");
        data.ShouldNotBeNull(
            $"o caddy não monta /data (montagens: {string.Join(", ", mounts.Select(m => $"{m.Source}→{m.Target}"))})");
        data.Type.ShouldBe(ContainerMountType.Volume);
        data.Source.ShouldBe("subsolo-caddy-data");

        // HTTPS fixo em 8443, sem o proxy do Aspire (como o Google falso): o endereço do navegador e do link mágico.
        var https = caddy.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(endpoint => endpoint.Name == "https");
        https.ShouldNotBeNull("o caddy não tem o endpoint https");
        https.UriScheme.ShouldBe("https");
        https.Port.ShouldBe(8443);
        https.TargetPort.ShouldBe(8443);
        https.IsProxied.ShouldBeFalse("o endpoint https do caddy deve ficar fora do proxy do Aspire (porta fixa no host)");

        // HTTP fixo em 8080 (redireciona para o HTTPS), também sem proxy, como no Compose.
        var http = caddy.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(endpoint => endpoint.Name == "http");
        http.ShouldNotBeNull("o caddy não tem o endpoint http");
        http.UriScheme.ShouldBe("http");
        http.Port.ShouldBe(8080);
        http.TargetPort.ShouldBe(8080);
        http.IsProxied.ShouldBeFalse("o endpoint http do caddy deve ficar fora do proxy do Aspire (porta fixa no host)");

        var (caddyEnvironment, caddyListing) = await EnvironmentOfAsync(caddy, executionContext, ct);
        caddyEnvironment.GetValueOrDefault("API_UPSTREAM").ShouldNotBeNull($"o caddy não recebe API_UPSTREAM:\n{caddyListing}");
        caddyEnvironment["API_UPSTREAM"].ShouldContain(
            "{api.bindings.http.", Case.Sensitive, $"API_UPSTREAM não referencia o endpoint http da api:\n{caddyListing}");
        caddyEnvironment.GetValueOrDefault("WEB_UPSTREAM").ShouldNotBeNull($"o caddy não recebe WEB_UPSTREAM:\n{caddyListing}");
        caddyEnvironment["WEB_UPSTREAM"].ShouldContain(
            "{web.bindings.http.", Case.Sensitive, $"WEB_UPSTREAM não referencia o endpoint http do web:\n{caddyListing}");

        // O endereço público passa a ser o do Caddy (link mágico, retorno do Google, Open Graph).
        var (apiEnvironment, apiListing) = await EnvironmentOfAsync(
            resources.Single(resource => resource.Name == "api"), executionContext, ct);
        apiEnvironment.GetValueOrDefault("App__PublicUrl").ShouldBe(PublicUrl, $"App__PublicUrl da api:\n{apiListing}");
        var (webEnvironment, webListing) = await EnvironmentOfAsync(
            resources.Single(resource => resource.Name == "web"), executionContext, ct);
        webEnvironment.GetValueOrDefault("SUBSOLO_PUBLIC_URL").ShouldBe(PublicUrl, $"SUBSOLO_PUBLIC_URL do web:\n{webListing}");
    }

    /// <summary>
    /// Variáveis de ambiente de <paramref name="resource"/> como o orquestrador as montaria no modo run: cada valor
    /// que não é texto vira a expressão de manifesto (<see cref="IManifestExpressionProvider.ValueExpression"/>).
    /// </summary>
    private static async Task<(Dictionary<string, string> Environment, string Listing)> EnvironmentOfAsync(
        IResource resource, DistributedApplicationExecutionContext executionContext, CancellationToken ct)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        if (resource.TryGetEnvironmentVariables(out var callbacks))
        {
            var context = new EnvironmentCallbackContext(executionContext, resource, values, ct);
            foreach (var callback in callbacks)
            {
                await callback.Callback(context);
            }
        }

        var environment = values.ToDictionary(
            variable => variable.Key,
            variable => variable.Value switch
            {
                string text => text,
                IManifestExpressionProvider expression => expression.ValueExpression,
                _ => variable.Value?.ToString() ?? string.Empty,
            },
            StringComparer.Ordinal);
        return (environment, string.Join("\n", environment.Select(variable => $"{variable.Key}={variable.Value}")));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Subsolo.slnx não encontrado acima de {AppContext.BaseDirectory}.");
    }
}

/// <summary>
/// SPEC-0040 IT-01 — com o <c>Subsolo.AppHost</c> no ar em modo run (banco descartável), a aplicação inteira responde
/// pelo Caddy em <c>https://localhost:8443</c>: o link mágico pedido por lá (com o CSRF) chega ao Mailpit apontando para
/// o 8443, é consumido pelo 8443, e <c>GET /api/v1/me</c> com o cookie de sessão (<c>__Host-subsolo</c>, Secure)
/// responde 200 com a conta; <c>GET /media/flyers/&lt;inexistente&gt;</c> dá o 404 do Caddy (Emenda 1); <c>GET /</c>
/// devolve a página do web. O certificado é da CA interna do Caddy (<c>tls internal</c>): o cliente do teste o aceita só
/// para <c>localhost</c> e com o emissor "Caddy Local Authority". Exige Docker, Node.js e pnpm, e as portas 8443, 8080
/// e 8090 livres: conflita com o <c>tools/compose-up.sh</c> e com um <c>aspire run</c> aberto. Usa o mesmo volume
/// <c>subsolo-caddy-data</c> (a CA interna) do <c>aspire run</c> do desenvolvedor. Roda na coleção
/// <see cref="AppHostRunTests"/>, sem paralelismo.
/// </summary>
[Collection(nameof(AppHostRunTests))]
public sealed partial class RunModeCaddyTests
{
    private const string PublicUrl = "https://localhost:8443";

    /// <summary>Folga para baixar imagens e instalar as dependências do front na primeira execução.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Prazo para o Caddy responder depois de api e web saudáveis, e para o e-mail chegar ao Mailpit.</summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0040:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task MagicLinkSignInAndWebPageWorkThroughTheCaddyOnHttps8443()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.StartAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("web", ct).WaitAsync(StartupTimeout, ct);

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            // Só o certificado da CA interna do Caddy em localhost; qualquer outro host segue a validação normal.
            ServerCertificateCustomValidationCallback = (message, _, chain, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None
                || (string.Equals(message.RequestUri?.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    && IssuedByCaddyLocalAuthority(chain)),
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(PublicUrl), Timeout = TimeSpan.FromSeconds(60) };

        await WaitForCaddyAsync(client, ct);

        // Pede o link mágico pelo Caddy, com o token anti-forgery (cookie __Host-subsolo-csrf + cabeçalho X-CSRF).
        var email = $"spec0040-{Guid.NewGuid():N}@subsolo.test";
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/magic-link")
        {
            Content = JsonContent.Create(new { email }),
        })
        {
            request.Headers.Add("X-CSRF", await AntiforgeryTokenAsync(client, ct));
            using var response = await client.SendAsync(request, ct);
            response.StatusCode.ShouldBe(
                HttpStatusCode.Accepted,
                $"POST {PublicUrl}/api/v1/auth/magic-link: {await response.Content.ReadAsStringAsync(ct)}");
        }

        // O link chega ao Mailpit apontando para o Caddy (App__PublicUrl = https://localhost:8443).
        using var mailpit = app.CreateHttpClient("mailpit", "http");
        var link = await ReadMagicLinkAsync(mailpit, email, ct);
        link.ShouldStartWith($"{PublicUrl}/entrar/confirmar?token=", Case.Sensitive);

        // A página do link (servida pelo web, pelo Caddy) e o consumo do token pela API.
        using (var page = await client.GetAsync(new Uri(link), ct))
        {
            page.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {link}");
        }

        var token = Uri.UnescapeDataString(new Uri(link).Query.TrimStart('?').Split('&')
            .Single(pair => pair.StartsWith("token=", StringComparison.Ordinal))["token=".Length..]);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/magic-link/consume")
        {
            Content = JsonContent.Create(new { token }),
        })
        {
            request.Headers.Add("X-CSRF", await AntiforgeryTokenAsync(client, ct));
            using var response = await client.SendAsync(request, ct);
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"POST {PublicUrl}/api/v1/auth/magic-link/consume: {await response.Content.ReadAsStringAsync(ct)}");
        }

        // A sessão vale pelo Caddy: /api/v1/me responde com a conta.
        using (var me = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative), ct))
        {
            var body = await me.Content.ReadAsStringAsync(ct);
            me.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {PublicUrl}/api/v1/me: {body}");
            using var account = JsonDocument.Parse(body);
            account.RootElement.GetProperty("email").GetString().ShouldBe(email, StringCompareShould.IgnoreCase);
        }

        // Emenda 1 — /media/* sai do Caddy (artifacts/media montado só leitura em /srv/media), não da API: um flyer
        // inexistente dá o 404 do file_server do Caddy (sem corpo de ProblemDetails da API).
        using (var media = await client.GetAsync(
            new Uri($"/media/flyers/spec0040-inexistente-{Guid.NewGuid():N}.webp", UriKind.Relative), ct))
        {
            var body = await media.Content.ReadAsStringAsync(ct);
            media.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"GET {PublicUrl}/media/flyers/<inexistente>: {body}");
            media.Headers.Server.ToString().ShouldContain("Caddy", Case.Sensitive);
            media.Content.Headers.ContentType?.MediaType.ShouldNotBe(
                "application/problem+json", "o 404 de /media/* veio da API, não do Caddy");
            body.ShouldBeEmpty("o 404 de /media/* deveria ser o do file_server do Caddy, sem corpo");
        }

        // O resto vai para o web: a página inicial em HTML.
        using (var home = await client.GetAsync(new Uri("/", UriKind.Relative), ct))
        {
            var html = await home.Content.ReadAsStringAsync(ct);
            home.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {PublicUrl}/");
            home.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
            html.ShouldContain("Subsolo", Case.Sensitive);
        }
    }

    /// <summary>Espera o Caddy responder em 8443 (o contêiner pode subir depois de api e web saudáveis).</summary>
    private static async Task WaitForCaddyAsync(HttpClient client, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + ReadyTimeout;
        string? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                last = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (HttpRequestException exception)
            {
                last = exception.Message;
            }
            catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
            {
                // Timeout do HttpClient (o Caddy aceitou a conexão mas ainda não respondeu): tenta de novo.
                last = exception.Message;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        throw new ShouldAssertException($"nada responde 200 em {PublicUrl}/health em {ReadyTimeout} (último: {last})");
    }

    /// <summary>A cadeia do certificado vem da CA interna do Caddy (emissor "Caddy Local Authority").</summary>
    private static bool IssuedByCaddyLocalAuthority(System.Security.Cryptography.X509Certificates.X509Chain? chain) =>
        chain is not null
        && chain.ChainElements.Any(element =>
            element.Certificate.Issuer.Contains("Caddy Local Authority", StringComparison.Ordinal));

    /// <summary><c>GET /api/v1/antiforgery</c>: grava o cookie do anti-forgery e devolve o token do cabeçalho X-CSRF.</summary>
    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri("/api/v1/antiforgery", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {PublicUrl}/api/v1/antiforgery: {body}");
        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("token").GetString();
        token.ShouldNotBeNullOrWhiteSpace();
        return token;
    }

    /// <summary>O link mágico do e-mail mandado para <paramref name="email"/>, lido na API HTTP do Mailpit.</summary>
    private static async Task<string> ReadMagicLinkAsync(HttpClient mailpit, string email, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + ReadyTimeout;
        var query = Uri.EscapeDataString($"to:\"{email}\"");
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var search = await mailpit.GetAsync(new Uri($"/api/v1/search?query={query}&limit=50", UriKind.Relative), ct);
            if (search.IsSuccessStatusCode)
            {
                using var result = JsonDocument.Parse(await search.Content.ReadAsStringAsync(ct));
                foreach (var message in result.RootElement.GetProperty("messages").EnumerateArray())
                {
                    var id = message.GetProperty("ID").GetString()!;
                    using var detail = JsonDocument.Parse(await mailpit.GetStringAsync(
                        new Uri($"/api/v1/message/{Uri.EscapeDataString(id)}", UriKind.Relative), ct));
                    var text = detail.RootElement.TryGetProperty("Text", out var value) ? value.GetString() : null;
                    var link = MagicLinkPattern().Match(text ?? string.Empty);
                    if (link.Success)
                    {
                        return link.Value;
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new ShouldAssertException($"o e-mail do link mágico para {email} não chegou ao Mailpit em {ReadyTimeout}");
    }

    [GeneratedRegex("""https?://[^\s"<>]+/entrar/confirmar\?token=[^\s"<>]+""")]
    private static partial Regex MagicLinkPattern();
}
