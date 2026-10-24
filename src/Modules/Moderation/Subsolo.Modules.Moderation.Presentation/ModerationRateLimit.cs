using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Moderation.Presentation;

/// <summary>
/// SPEC-0033 (Emenda 1) — a política nomeada de rate limit da denúncia, no padrão do ADR-0003 (o rate limiting do
/// ASP.NET Core, com o 429 <c>security.rate_limited</c> do host): <see cref="DefaultPermitLimit"/> denúncias por fã numa
/// janela fixa de <see cref="DefaultWindow"/>, com a chave no id do usuário (outro fã no mesmo IP não é afetado). Os
/// limites vêm de <c>Security:RateLimiting:moderation-report:{PermitLimit,Window}</c> quando configurados.
/// </summary>
public static class ModerationRateLimit
{
    /// <summary>Nome da política (e da seção de configuração).</summary>
    public const string PolicyName = "moderation-report";

    /// <summary>Denúncias por fã na janela.</summary>
    public const int DefaultPermitLimit = 10;

    /// <summary>A janela fixa: um dia.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(1);

    /// <summary>Registra a política <see cref="PolicyName"/> nas opções de rate limiting do host.</summary>
    public static IServiceCollection AddModerationRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<RateLimiterOptions>(options =>
        {
            var rule = configuration.GetSection($"Security:RateLimiting:{PolicyName}").Get<RateLimitRule>()
                ?? new RateLimitRule(DefaultPermitLimit, DefaultWindow);
            options.AddPolicy(PolicyName, new PerUserPolicy(rule));
        });
        return services;
    }

    /// <summary>Os limites da política.</summary>
    internal sealed record RateLimitRule(int PermitLimit, TimeSpan Window);

    /// <summary>
    /// A partição é o id do usuário do cookie de sessão. O rate limit roda antes da autenticação do host, então o id vem do
    /// tíquete do cookie (cifrado e assinado pela Data Protection: não dá para forjar), lido com as mesmas opções do
    /// esquema; a validação da sessão no banco continua com a autenticação. Sem cookie válido, a chave é o IP (a
    /// requisição vai dar 401 de qualquer jeito).
    /// </summary>
    private sealed class PerUserPolicy(RateLimitRule rule) : IRateLimiterPolicy<string>
    {
        /// <summary>Sem tratador próprio: vale o 429 do host.</summary>
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rule.PermitLimit,
                    Window = rule.Window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });

        private static string PartitionKey(HttpContext httpContext) =>
            UserId(httpContext) is { } userId
                ? $"user:{userId}"
                : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString()}";

        private static string? UserId(HttpContext httpContext)
        {
            var options = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(SessionAuthentication.Scheme);
            if (options.Cookie.Name is not { } name
                || options.CookieManager.GetRequestCookie(httpContext, name) is not { Length: > 0 } cookie)
            {
                return null;
            }

            var principal = options.TicketDataFormat.Unprotect(cookie)?.Principal;
            return Guid.TryParse(
                principal?.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
                ? id.ToString()
                : null;
        }
    }
}
