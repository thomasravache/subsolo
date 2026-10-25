using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0041 UT-01 — no modelo do <c>Subsolo.AppHost</c> em modo run (montado pelo
/// <see cref="DistributedApplicationTestingBuilder"/>, sem subir os recursos), a opção <c>Subsolo:PublicUrl</c> (o
/// endereço do túnel, para abrir o ambiente local pelo celular) vira o <c>App__PublicUrl</c> da <c>api</c> e o
/// <c>SUBSOLO_PUBLIC_URL</c> do <c>web</c>, e o <c>web</c> recebe <c>SUBSOLO_DEV_ORIGINS</c> com o host dela (o
/// <c>allowedDevOrigins</c> do <c>next dev</c>). Sem a opção, o endereço é o do Caddy (<c>https://localhost:8443</c>,
/// SPEC-0040) e não há <c>SUBSOLO_DEV_ORIGINS</c>. Um valor que não seja URL absoluta <c>https</c> sem caminho para a
/// montagem com uma mensagem que cita a opção.
/// </summary>
public sealed class RunModePublicUrlModelTests
{
    private const string TunnelUrl = "https://celular.example";
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-01")]
    [Trait("Kind", "Integration")]
    public async Task PublicUrlOptionBecomesTheApiAndWebAddressAndTheDevOrigin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, web) = await EnvironmentsAsync(
            ["--Subsolo:EphemeralDatabase=true", $"--Subsolo:PublicUrl={TunnelUrl}"], ct);

        api.Environment.GetValueOrDefault("App__PublicUrl").ShouldBe(TunnelUrl, $"App__PublicUrl da api:\n{api.Listing}");
        web.Environment.GetValueOrDefault("SUBSOLO_PUBLIC_URL").ShouldBe(TunnelUrl, $"SUBSOLO_PUBLIC_URL do web:\n{web.Listing}");
        web.Environment.GetValueOrDefault("SUBSOLO_DEV_ORIGINS").ShouldBe(
            "celular.example", $"SUBSOLO_DEV_ORIGINS do web:\n{web.Listing}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutTheOptionTheAddressIsTheCaddyAndThereAreNoDevOrigins()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, web) = await EnvironmentsAsync(["--Subsolo:EphemeralDatabase=true"], ct);

        api.Environment.GetValueOrDefault("App__PublicUrl").ShouldBe(
            "https://localhost:8443", $"App__PublicUrl da api:\n{api.Listing}");
        web.Environment.GetValueOrDefault("SUBSOLO_PUBLIC_URL").ShouldBe(
            "https://localhost:8443", $"SUBSOLO_PUBLIC_URL do web:\n{web.Listing}");
        web.Environment.ContainsKey("SUBSOLO_DEV_ORIGINS").ShouldBeFalse(
            $"sem Subsolo:PublicUrl o web não deve receber SUBSOLO_DEV_ORIGINS:\n{web.Listing}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0041:UT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("http://celular.example")]
    [InlineData("https://celular.example/caminho")]
    public async Task InvalidPublicUrlStopsTheAppHostWithAClearMessage(string value)
    {
        var ct = TestContext.Current.CancellationToken;

        var exception = await Should.ThrowAsync<Exception>(
            () => EnvironmentsAsync(["--Subsolo:EphemeralDatabase=true", $"--Subsolo:PublicUrl={value}"], ct),
            $"o AppHost aceitou --Subsolo:PublicUrl={value} (deveria parar na montagem)");

        // H41-07 — o erro da montagem é um InvalidOperationException cuja mensagem cita a opção. Ele pode vir
        // embrulhado pelo teste do Aspire, por isso a busca percorre a cadeia de exceções.
        var chain = new List<Exception>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            chain.Add(current);
            if (current is AggregateException aggregate)
            {
                chain.AddRange(aggregate.Flatten().InnerExceptions);
            }
        }

        chain.OfType<InvalidOperationException>()
            .Any(invalid => invalid.Message.Contains("Subsolo:PublicUrl", StringComparison.Ordinal))
            .ShouldBeTrue($"nenhum InvalidOperationException com 'Subsolo:PublicUrl' na mensagem:\n{exception}");
    }

    private static async Task<(
        (Dictionary<string, string> Environment, string Listing) Api,
        (Dictionary<string, string> Environment, string Listing) Web)> EnvironmentsAsync(string[] args, CancellationToken ct)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(args, ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);
        var executionContext = app.Services.GetRequiredService<DistributedApplicationExecutionContext>();
        var resources = app.Services.GetRequiredService<DistributedApplicationModel>().Resources;
        var api = await RunModeCaddyModelTests.EnvironmentOfAsync(
            resources.Single(resource => resource.Name == "api"), executionContext, ct);
        var web = await RunModeCaddyModelTests.EnvironmentOfAsync(
            resources.Single(resource => resource.Name == "web"), executionContext, ct);
        return (api, web);
    }
}

/// <summary>
/// SPEC-0041 IT-01 — com o <c>Subsolo.AppHost</c> no ar em modo run (banco descartável) e
/// <c>--Subsolo:PublicUrl=https://celular.example</c> (o endereço do túnel), o navegador do celular, cuja origem é o
/// túnel, recebe os scripts de desenvolvimento do <c>next dev</c>: <c>GET /_next/static/…js</c> pelo Caddy com
/// <c>Origin: https://celular.example</c> responde 200 (sem a opção, 403); e o link mágico que chega ao Mailpit aponta
/// para o túnel. O certificado é o da CA interna do Caddy (como na SPEC-0040 IT-01). Exige Docker, Node.js e pnpm, e as
/// portas 8443, 8080 e 8090 livres; roda na coleção <see cref="AppHostRunTests"/>, sem paralelismo.
/// </summary>
[Collection(nameof(AppHostRunTests))]
public sealed partial class RunModePublicUrlTests
{
    private const string CaddyUrl = "https://localhost:8443";
    private const string TunnelUrl = "https://celular.example";

    /// <summary>Folga para baixar imagens e instalar as dependências do front na primeira execução.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    [Trait("Spec", "SPEC-0041:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task DevScriptsAndMagicLinkWorkFromTheTunnelOrigin()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true", $"--Subsolo:PublicUrl={TunnelUrl}"], ct);
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
                    && RunModeCaddyTests.IssuedByCaddyLocalAuthority(chain)),
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(CaddyUrl), Timeout = TimeSpan.FromSeconds(60) };

        await RunModeCaddyTests.WaitForCaddyAsync(client, ct);

        // A página de entrar, como o navegador do celular a pede pelo túnel (o túnel chega ao Caddy com Host localhost).
        string html;
        using (var request = FromTheTunnel(HttpMethod.Get, "/entrar"))
        using (var page = await client.SendAsync(request, ct))
        {
            html = await page.Content.ReadAsStringAsync(ct);
            page.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {CaddyUrl}/entrar com Origin {TunnelUrl}");
        }

        var script = DevScriptPattern().Match(html);
        script.Success.ShouldBeTrue($"a página /entrar não tem nenhum script /_next/static/…js:\n{html}");
        var scriptPath = WebUtility.HtmlDecode(script.Groups["path"].Value);

        // O script de desenvolvimento do next dev, com a origem do túnel: 200 (o next dev bloqueia com 403 as origens
        // fora de localhost e do allowedDevOrigins).
        using (var request = FromTheTunnel(HttpMethod.Get, scriptPath))
        using (var response = await client.SendAsync(request, ct))
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"GET {CaddyUrl}{scriptPath} com Origin {TunnelUrl}: {body[..Math.Min(body.Length, 500)]}");
        }

        // O link mágico pedido pelo Caddy (com o CSRF) chega ao Mailpit apontando para o túnel.
        var email = $"spec0041-{Guid.NewGuid():N}@subsolo.test";
        using (var request = FromTheTunnel(HttpMethod.Post, "/api/v1/auth/magic-link"))
        {
            request.Content = JsonContent.Create(new { email });
            request.Headers.Add("X-CSRF", await RunModeCaddyTests.AntiforgeryTokenAsync(client, ct));
            using var response = await client.SendAsync(request, ct);
            response.StatusCode.ShouldBe(
                HttpStatusCode.Accepted,
                $"POST {CaddyUrl}/api/v1/auth/magic-link: {await response.Content.ReadAsStringAsync(ct)}");
        }

        using var mailpit = app.CreateHttpClient("mailpit", "http");
        var link = await RunModeCaddyTests.ReadMagicLinkAsync(mailpit, email, ct);
        link.ShouldStartWith($"{TunnelUrl}/entrar/confirmar", Case.Sensitive);
    }

    /// <summary>Uma requisição com os cabeçalhos que o navegador manda numa página aberta pelo túnel.</summary>
    private static HttpRequestMessage FromTheTunnel(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Origin", TunnelUrl);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        return request;
    }

    [GeneratedRegex(@"(?:src|href)=""(?<path>/_next/static/[^""]+?\.js(?:\?[^""]*)?)""")]
    private static partial Regex DevScriptPattern();
}
