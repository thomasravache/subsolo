using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Presentation.Features.SignInWithGoogle;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0021 IT-06 (§7.6, herança da revisão da SPEC-0020) — as <see cref="OpenIdConnectOptions"/> do esquema
/// <c>google</c>, montadas pelo registro do módulo: fluxo de código (<c>response_type=code</c>) com PKCE, escopos
/// <c>openid email</c>, nada guardado do provedor (<c>SaveTokens=false</c>, sem userinfo), a validação padrão do
/// <c>id_token</c> (emissor, audiência, validade e assinatura), HTTPS na descoberta fora de Development, e os caminhos de
/// sign-out remoto do handler (<c>RemoteSignOutPath</c>, <c>SignedOutCallbackPath</c>) sob
/// <c>/api/v1/auth/google/</c>, sem uso (nenhum outro endpoint responde neles).
/// </summary>
public sealed class GoogleOpenIdConnectOptionsTests
{
    private const string GooglePrefix = "/api/v1/auth/google/";

    private static readonly GoogleOptions Google = new()
    {
        Authority = "https://accounts.google.test",
        ClientId = "subsolo-unit",
        ClientSecret = "segredo-do-teste-unitario",
    };

    private static OpenIdConnectOptions Options(string environment = "Production")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(environment));
        services.AddOptions<UsersOptions>().Configure(options => options.Google = Google);
        services.AddAuthentication().AddGoogleSignIn(Google);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(GoogleSignIn.Scheme);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    public void TheCodeFlowUsesPkceAndStoresNothingFromTheProvider()
    {
        var options = Options();

        options.ResponseType.ShouldBe(OpenIdConnectResponseType.Code);
        options.UsePkce.ShouldBeTrue("PKCE no fluxo de código");
        options.SaveTokens.ShouldBeFalse("nenhum token do provedor é guardado");
        options.GetClaimsFromUserInfoEndpoint.ShouldBeFalse("o userinfo não é consultado");
        options.Scope.ShouldBe(["openid", "email"], ignoreOrder: true);
        options.Authority.ShouldBe(Google.Authority);
        options.ClientId.ShouldBe(Google.ClientId);
        options.CallbackPath.Value.ShouldBe("/api/v1/auth/google/callback");
        options.RequireHttpsMetadata.ShouldBeTrue("fora de Development a descoberta exige HTTPS");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    public void TheIdTokenKeepsTheDefaultValidation()
    {
        var validation = Options().TokenValidationParameters;

        validation.ValidateIssuer.ShouldBeTrue("o emissor do id_token é conferido");
        validation.ValidateAudience.ShouldBeTrue("a audiência do id_token é conferida");
        validation.ValidateLifetime.ShouldBeTrue("a validade do id_token é conferida");
        validation.RequireSignedTokens.ShouldBeTrue("o id_token precisa ser assinado");
        validation.RequireExpirationTime.ShouldBeTrue("o id_token precisa ter exp");
        validation.ValidAudience.ShouldBeOneOf(Google.ClientId, null);
        validation.SignatureValidator.ShouldBeNull("sem validação de assinatura própria (a padrão confere a chave do provedor)");
        validation.IssuerSigningKeyValidator.ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    public void TheRemoteSignOutPathsAreUnusedPathsUnderTheGooglePrefix()
    {
        var options = Options();

        options.RemoteSignOutPath.Value.ShouldNotBeNull().ShouldStartWith(GooglePrefix, Case.Sensitive,
            $"RemoteSignOutPath fora de {GooglePrefix}: {options.RemoteSignOutPath}");
        options.SignedOutCallbackPath.Value.ShouldNotBeNull().ShouldStartWith(GooglePrefix, Case.Sensitive,
            $"SignedOutCallbackPath fora de {GooglePrefix}: {options.SignedOutCallbackPath}");
        string[] used = ["/api/v1/auth/google", "/api/v1/auth/google/callback", "/api/v1/auth/providers", "/api/v1/auth/logout"];
        used.ShouldNotContain(options.RemoteSignOutPath.Value!, "RemoteSignOutPath num caminho já usado");
        used.ShouldNotContain(options.SignedOutCallbackPath.Value!, "SignedOutCallbackPath num caminho já usado");
        options.RemoteSignOutPath.ShouldNotBe(options.SignedOutCallbackPath);
        options.SignedOutCallbackPath.ShouldNotBe(options.CallbackPath);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    public void HttpDiscoveryIsAcceptedOnlyInDevelopment()
    {
        var google = new GoogleOptions { Authority = "http://127.0.0.1:8090/default", ClientId = "subsolo-local" };
        OpenIdConnectOptions Build(string environment)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddSingleton<IHostEnvironment>(new TestEnvironment(environment));
            services.AddOptions<UsersOptions>().Configure(options => options.Google = google);
            services.AddAuthentication().AddGoogleSignIn(google);
            return services.BuildServiceProvider()
                .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(GoogleSignIn.Scheme);
        }

        Build("Development").RequireHttpsMetadata.ShouldBeFalse("o provedor falso local roda em http só em Development");
        Should.Throw<InvalidOperationException>(() => Build("Production"), "fora de Development, authority http é recusada");
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Subsolo.Users.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
