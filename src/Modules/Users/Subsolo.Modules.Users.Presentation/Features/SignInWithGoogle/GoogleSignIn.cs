using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.SignInWithGoogle;

namespace Subsolo.Modules.Users.Presentation.Features.SignInWithGoogle;

/// <summary>
/// SPEC-0020 — esquema OpenID Connect <see cref="Scheme"/> (Entrar com Google, ADR-0003), registrado só quando
/// <c>Users:Google</c> tem Authority e ClientId (<see cref="GoogleOptions.IsEnabled"/>): fluxo de código com PKCE,
/// <c>state</c> e <c>nonce</c> do handler oficial, escopos <c>openid email</c>, nada guardado do provedor (sem tokens,
/// sem userinfo). O <c>SignInScheme</c> é o cookie externo temporário <see cref="ExternalCookieName"/> (10 minutos),
/// que nunca chega a ser emitido: no <c>OnTicketReceived</c> o <see cref="GoogleSignInCallback"/> entra pelo
/// <c>ISignInService</c> (cookie <c>__Host-subsolo</c>), o cookie externo é apagado e o navegador volta para o
/// <c>returnTo</c> saneado. Qualquer falha do retorno volta para <see cref="GoogleSignInCallback.ProviderErrorRedirect"/>.
/// Os logs de falha levam só o motivo (<c>state</c>, <c>email_not_verified</c>, <c>provider_error</c>), sem e-mail.
/// </summary>
internal static partial class GoogleSignIn
{
    /// <summary>Esquema OIDC do Google.</summary>
    public const string Scheme = "google";

    /// <summary>Esquema do cookie externo (o <c>SignInScheme</c> do handler OIDC).</summary>
    public const string ExternalScheme = "google-external";

    /// <summary>Cookie externo do retorno: <c>__Host-</c>, HttpOnly, 10 minutos e apagado no fim.</summary>
    public const string ExternalCookieName = "__Host-subsolo-ext";

    /// <summary>Retorno do provedor, tratado pelo handler OIDC (fora do contrato publicado).</summary>
    public const string CallbackPath = "/api/v1/auth/google/callback";

    /// <summary>Chave do <c>returnTo</c> nas <see cref="AuthenticationProperties"/> (protegidas no <c>state</c>).</summary>
    public const string ReturnToItem = "subsolo.returnTo";

    /// <summary>Validade do cookie externo e das <see cref="AuthenticationProperties"/> do desafio.</summary>
    public static readonly TimeSpan ExternalLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Tempo máximo de cada chamada ao provedor pelo canal de fundo (descoberta, chaves, token).</summary>
    public static readonly TimeSpan BackchannelTimeout = TimeSpan.FromSeconds(10);

    internal const string ReasonState = "state";

    internal const string ReasonEmailNotVerified = "email_not_verified";

    internal const string ReasonProviderError = "provider_error";

    private const string LoggerCategory = "Subsolo.Modules.Users.Presentation.Features.SignInWithGoogle.GoogleSignIn";

    /// <summary>
    /// Registra o cookie externo e o esquema <see cref="Scheme"/> quando <paramref name="google"/> está ligado; sem
    /// configuração, nada é registrado (o botão some e o início responde 404).
    /// </summary>
    public static AuthenticationBuilder AddGoogleSignIn(this AuthenticationBuilder builder, GoogleOptions google)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(google);
        if (!google.IsEnabled)
        {
            return builder;
        }

        builder.AddCookie(ExternalScheme, options =>
        {
            options.Cookie.Name = ExternalCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = ExternalLifetime;
            options.SlidingExpiration = false;
        });
        builder.AddOpenIdConnect(Scheme, _ => { });
        builder.Services.AddOptions<OpenIdConnectOptions>(Scheme)
            .Configure<IOptions<UsersOptions>, IHostEnvironment>((options, users, environment) =>
                Configure(options, users.Value.Google, environment));
        return builder;
    }

    private static void Configure(OpenIdConnectOptions options, GoogleOptions google, IHostEnvironment environment)
    {
        options.Authority = google.Authority;
        options.ClientId = google.ClientId;
        options.ClientSecret = google.ClientSecret;

        // http só para o provedor falso local (AppHost em run, Compose local, testes), sempre em Development.
        options.RequireHttpsMetadata = !(environment.IsDevelopment()
            && Uri.TryCreate(google.Authority, UriKind.Absolute, out var authority)
            && authority.Scheme == Uri.UriSchemeHttp);

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;
        options.Scope.Clear();
        options.Scope.Add(OpenIdConnectScope.OpenId);
        options.Scope.Add(OpenIdConnectScope.Email);
        options.CallbackPath = CallbackPath;
        options.SignInScheme = ExternalScheme;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.RemoteAuthenticationTimeout = ExternalLifetime;
        options.BackchannelTimeout = BackchannelTimeout;

        // O retorno chega por POST de formulário vindo do provedor (outro site): os cookies de correlação e do nonce
        // precisam de SameSite=None (e por isso Secure).
        options.CorrelationCookie.Name = "__Secure-subsolo-correlation.";
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.Name = "__Secure-subsolo-nonce.";
        options.NonceCookie.HttpOnly = true;
        options.NonceCookie.SameSite = SameSiteMode.None;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

        options.Events.OnTicketReceived = TicketReceivedAsync;
        options.Events.OnRemoteFailure = RemoteFailureAsync;
    }

    private static async Task TicketReceivedAsync(TicketReceivedContext context)
    {
        var httpContext = context.HttpContext;
        var returnTo = context.Properties?.Items.TryGetValue(ReturnToItem, out var value) == true ? value : null;
        var callback = httpContext.RequestServices.GetRequiredService<GoogleSignInCallback>();
        var principal = context.Principal ?? new System.Security.Claims.ClaimsPrincipal();

        var outcome = await callback.CompleteAsync(principal, returnTo, httpContext.RequestAborted);
        if (!outcome.SignedIn)
        {
            LogRejected(Logger(httpContext), ReasonEmailNotVerified);
        }

        DeleteExternalCookie(httpContext);
        httpContext.Response.Redirect(outcome.RedirectTo);
        context.HandleResponse();
    }

    private static Task RemoteFailureAsync(RemoteFailureContext context)
    {
        var reason = IsStateFailure(context.Failure) ? ReasonState : ReasonProviderError;
        LogRejected(Logger(context.HttpContext), reason);
        DeleteExternalCookie(context.HttpContext);
        context.Response.Redirect(GoogleSignInCallback.ProviderErrorRedirect);
        context.HandleResponse();
        return Task.CompletedTask;
    }

    /// <summary>O provedor ficou fora do ar no início do login (descoberta ou chaves inacessíveis).</summary>
    internal static void LogProviderUnavailable(HttpContext httpContext) =>
        LogRejected(Logger(httpContext), ReasonProviderError);

    /// <summary><c>state</c> ausente, adulterado ou sem a correlação (as mensagens do handler OIDC).</summary>
    private static bool IsStateFailure(Exception? failure) =>
        failure?.Message is { } message
        && (message.Contains("state", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Correlation", StringComparison.OrdinalIgnoreCase));

    private static void DeleteExternalCookie(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(ExternalCookieName, new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });

    private static ILogger Logger(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada com Google recusada: {GoogleFailureReason}")]
    private static partial void LogRejected(ILogger logger, string googleFailureReason);
}
