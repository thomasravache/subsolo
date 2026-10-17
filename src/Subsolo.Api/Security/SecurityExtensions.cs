using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Api.Security;

/// <summary>
/// SPEC-0010 — segurança base da API (ADR-0003): sessão por cookie <c>__Host-subsolo</c>, autorização negada por padrão
/// (política de fallback = usuário autenticado) com as políticas <c>fan</c>, <c>artist</c> e <c>admin</c>, 401/403 em
/// ProblemDetails (nunca redirecionamento), anti-forgery por cabeçalho <c>X-CSRF</c> e rate limiting por IP.
/// SPEC-0014 — o cookie é validado contra a sessão no banco (<see cref="SessionCookieValidation"/>), o banco fora do ar
/// na validação vira 503, os endpoints marcados com <see cref="NoStoreMetadata"/> respondem <c>Cache-Control: no-store</c>
/// e o token anti-forgery usa o rate limit <c>default</c>.
/// </summary>
internal static class SecurityExtensions
{
    /// <summary>Cookie de sessão (ADR-0003): prefixo <c>__Host-</c>, HttpOnly, Secure e SameSite=Lax.</summary>
    public const string SessionCookieName = "__Host-subsolo";

    /// <summary>Cookie do anti-forgery: par do token enviado no cabeçalho <see cref="AntiforgeryFilter.HeaderName"/>.</summary>
    public const string AntiforgeryCookieName = "__Host-subsolo-csrf";

    /// <summary>
    /// SPEC-0016 — validade do cookie de sessão: o teto absoluto da sessão no banco (90 dias desde a entrada). Antes
    /// disso, o cookie só deixa de valer quando a sessão do banco expira ou é revogada.
    /// </summary>
    public static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(90);

    /// <summary>Endpoint que emite o token anti-forgery (fora do OpenAPI).</summary>
    public const string AntiforgeryRoute = "/api/v1/antiforgery";

    /// <summary>Seção de configuração que sobrescreve os limites padrão (ex.: <c>Security:RateLimiting:auth:PermitLimit</c>).</summary>
    public const string RateLimitingSection = "Security:RateLimiting";

    /// <summary>Redes privadas (a rede interna do Compose) de onde o X-Forwarded-For do Caddy é aceito.</summary>
    private static readonly string[] PrivateNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7"];

    private static readonly Dictionary<string, RateLimitRule> DefaultRateLimits = new(StringComparer.Ordinal)
    {
        [RateLimitPolicies.Auth] = new(5, TimeSpan.FromMinutes(1)),
        [RateLimitPolicies.Report] = new(10, TimeSpan.FromHours(1)),
        [RateLimitPolicies.Default] = new(100, TimeSpan.FromMinutes(1)),
    };

    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                // SPEC-0016 (§7.6 a): quem manda na validade é a sessão do banco (30 dias sem uso, teto de 90 desde a
                // entrada). O cookie vale até o teto e não tem janela deslizante própria: o sliding do ASP.NET só
                // renovava depois de metade do prazo e expirava o cookie antes de a sessão expirar.
                options.ExpireTimeSpan = SessionCookieLifetime;
                options.SlidingExpiration = false;

                // SPEC-0014: o cookie só vale com a sessão do banco por trás (revogável); os papéis vêm dela.
                options.Events.OnValidatePrincipal = SessionCookieValidation.ValidatePrincipalAsync;

                // API: o desafio e a proibição respondem ProblemDetails, nunca 302 para uma página de login.
                options.Events.OnRedirectToLogin = context => RejectAsync(context.HttpContext, SecurityProblems.Unauthorized(), SecurityProblems.AuthRequired);
                options.Events.OnRedirectToAccessDenied = context => RejectAsync(context.HttpContext, SecurityProblems.Forbidden(), SecurityProblems.AuthForbidden);
            });

        // As políticas não fixam o esquema: valem para o esquema padrão de autenticação (o dos testes também).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Fan, policy => policy.RequireAuthenticatedUser().RequireRole(AuthorizationPolicies.Fan))
            .AddPolicy(AuthorizationPolicies.Artist, policy => policy.RequireAuthenticatedUser().RequireRole(AuthorizationPolicies.Artist))
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole(AuthorizationPolicies.Admin));

        services.AddExceptionHandler<SessionStoreUnavailableExceptionHandler>();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryFilter.HeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await RejectAsync(context.HttpContext, SecurityProblems.TooManyRequests(), SecurityProblems.RateLimited);
            };

            foreach (var (policy, fallback) in DefaultRateLimits)
            {
                var rule = configuration.GetSection($"{RateLimitingSection}:{policy}").Get<RateLimitRule>() ?? fallback;
                options.AddPolicy(policy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    ClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rule.PermitLimit,
                        Window = rule.Window,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
            }
        });

        // Atrás do Caddy (ADR-0004): o IP do cliente vem do X-Forwarded-For quando a conexão chega de uma rede privada
        // (a rede do Compose); de qualquer outra origem o cabeçalho é ignorado.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var network in PrivateNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>Middlewares de segurança, depois do tratamento de exceções e antes dos endpoints.</summary>
    public static WebApplication UseApiSecurity(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use(ApplyNoStore);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary><c>GET /api/v1/antiforgery</c>: público, emite o cookie e devolve <c>{token}</c> para o cabeçalho X-CSRF.</summary>
    public static IEndpointRouteBuilder MapAntiforgeryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(AntiforgeryRoute, (HttpContext httpContext, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(httpContext);
                return TypedResults.Ok(new AntiforgeryToken(tokens.RequestToken!));
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .ExcludeFromDescription();
        return app;
    }

    /// <summary>
    /// <c>Cache-Control: no-store</c> em toda resposta de endpoint com <see cref="NoStoreMetadata"/> — inclusive as que
    /// saem antes do endpoint (429 do rate limit, 401 do desafio).
    /// </summary>
    private static Task ApplyNoStore(HttpContext httpContext, RequestDelegate next)
    {
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<NoStoreMetadata>() is not null)
        {
            httpContext.Response.OnStarting(() =>
            {
                httpContext.Response.Headers.CacheControl = NoStoreMetadata.CacheControl;
                return Task.CompletedTask;
            });
        }

        return next(httpContext);
    }

    private static async Task RejectAsync(HttpContext httpContext, IResult problem, string code)
    {
        SecurityLog.Rejected(httpContext, code);
        await problem.ExecuteAsync(httpContext);
    }

    private static string ClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();

    private sealed record RateLimitRule(int PermitLimit, TimeSpan Window);

    private sealed record AntiforgeryToken(string Token);
}
