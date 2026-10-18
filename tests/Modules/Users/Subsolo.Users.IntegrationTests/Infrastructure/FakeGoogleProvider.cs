using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Subsolo.Users.IntegrationTests.Infrastructure;

// SPEC-0020 — um provedor OpenID Connect falso real (container efêmero) para todo o assembly, no papel do Google.
[assembly: AssemblyFixture(typeof(FakeGoogleProvider))]

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0020 — o provedor OIDC falso (<see cref="Image"/>, mock-oauth2-server) sem login interativo: o authorize emite o
/// código na hora e o token devolve as claims fixas do emissor (<see cref="Issuers"/>). O emissor é o primeiro segmento
/// do caminho (<c>/{issuer}/.well-known/openid-configuration</c>), e a URL vista pelo teste (o "navegador") é a mesma
/// que a API usa no canal de fundo — o <c>iss</c> do <c>id_token</c> bate com a descoberta.
/// </summary>
public sealed class FakeGoogleProvider : IAsyncLifetime
{
    /// <summary>Imagem com tag fixa (ADR-0009): mock-oauth2-server 6.0.4, publicada em 2026-09-29 (licença MIT).</summary>
    public const string Image = "ghcr.io/navikt/mock-oauth2-server:6.0.4";

    /// <summary>E-mail verificado devolvido pelo emissor <see cref="Issuers.Verified"/>.</summary>
    public const string VerifiedEmail = "fa@subsolo.test";

    /// <summary>E-mail não verificado devolvido pelo emissor <see cref="Issuers.Unverified"/>.</summary>
    public const string UnverifiedEmail = "nao-verificado@subsolo.test";

    /// <summary>Nome e foto devolvidos junto (o Subsolo descarta: só o e-mail é usado).</summary>
    public const string GoogleName = "Fã do Google Falso";

    private const int ContainerPort = 8080;

    private IContainer? _container;

    /// <summary>Os emissores configurados, um por cenário de claims.</summary>
    public static class Issuers
    {
        /// <summary><c>email</c> = <see cref="VerifiedEmail"/>, <c>email_verified</c> = <c>true</c>.</summary>
        public const string Verified = "google-verificado";

        /// <summary><c>email</c> = <see cref="UnverifiedEmail"/>, <c>email_verified</c> = <c>false</c>.</summary>
        public const string Unverified = "google-nao-verificado";

        /// <summary>Sem <c>email</c> (só <c>sub</c> e <c>email_verified</c> = <c>true</c>).</summary>
        public const string WithoutEmail = "google-sem-email";
    }

    /// <summary>Base do servidor (<c>http://host:porta</c>), válida depois de <see cref="InitializeAsync"/>.</summary>
    public Uri BaseAddress => new(
        $"http://{Container.Hostname}:{Container.GetMappedPublicPort(ContainerPort)}",
        UriKind.Absolute);

    private IContainer Container => _container
        ?? throw new InvalidOperationException("FakeGoogleProvider ainda não inicializado (InitializeAsync).");

    /// <summary>A Authority (<c>Users:Google:Authority</c>) do emissor <paramref name="issuer"/>.</summary>
    public string Authority(string issuer) => new Uri(BaseAddress, issuer).ToString();

    public async ValueTask InitializeAsync()
    {
        var container = new ContainerBuilder(Image)
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .WithEnvironment("SERVER_PORT", ContainerPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithEnvironment("JAVA_TOOL_OPTIONS", "-Xmx128m")
            .WithEnvironment("JSON_CONFIG", Configuration())
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(ContainerPort).ForPath("/isalive")))
            .Build();
        await container.StartAsync();
        _container = container;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    /// <summary>
    /// <c>JSON_CONFIG</c>: sem login interativo e, por emissor, as claims de qualquer código trocado no token
    /// (vão no <c>id_token</c>, com o <c>nonce</c> do authorize).
    /// </summary>
    private static string Configuration() => JsonSerializer.Serialize(new
    {
        interactiveLogin = false,
        httpServer = "NettyWrapper",
        tokenCallbacks = new object[]
        {
            Callback(Issuers.Verified, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = "google-sub-verificado",
                ["email"] = VerifiedEmail,
                ["email_verified"] = true,
                ["name"] = GoogleName,
                ["picture"] = "https://fotos.google.test/fa.png",
            }),
            Callback(Issuers.Unverified, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = "google-sub-nao-verificado",
                ["email"] = UnverifiedEmail,
                ["email_verified"] = false,
            }),
            Callback(Issuers.WithoutEmail, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = "google-sub-sem-email",
                ["email_verified"] = true,
            }),
        },
    });

    private static object Callback(string issuer, Dictionary<string, object> claims) => new
    {
        issuerId = issuer,
        requestMappings = new[] { new { requestParam = "code", match = "*", claims } },
    };
}
