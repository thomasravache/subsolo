using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Xunit.Sdk;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0014 — regra de rate limit (ADR-0003): todo endpoint da API declara uma política de rate limiting
/// (<c>RequireRateLimiting(&lt;política&gt;)</c>), exceto as sondas <c>/health</c> e <c>/alive</c> (monitor e orquestrador)
/// e o <c>/openapi/*</c>, que só existe em desenvolvimento (Emenda v2).
/// Os endpoints vêm de <see cref="EndpointAuthorizationRules.EndpointsOf"/> e
/// <see cref="EndpointAuthorizationRules.ModuleEndpoints"/>.
/// </summary>
public static class EndpointRateLimitRules
{
    /// <summary>Texto da regra, citado na falha.</summary>
    public const string Rule =
        "ADR-0003: todo endpoint da API declara uma política de rate limit (RequireRateLimiting), exceto /health, /alive e /openapi/*";

    /// <summary>Rotas dispensadas da regra (exatas).</summary>
    public static readonly IReadOnlySet<string> Exempt = new HashSet<string>(StringComparer.Ordinal) { "/health", "/alive" };

    /// <summary>Prefixo dispensado: o documento OpenAPI, mapeado só em desenvolvimento.</summary>
    public const string ExemptPrefix = "/openapi/";

    /// <summary>A rota está dispensada da regra.</summary>
    public static bool IsExempt(string? route) =>
        route is not null && (Exempt.Contains(route) || route.StartsWith(ExemptPrefix, StringComparison.Ordinal));

    /// <summary>A política de rate limit declarada no endpoint, ou <see langword="null"/>.</summary>
    public static string? PolicyOf(Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null
            ? endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            : null;
    }

    /// <summary>Falha (exceção) nomeando cada endpoint não dispensado (<see cref="IsExempt"/>) sem política de rate limit.</summary>
    public static void Check(IEnumerable<RouteEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var undeclared = endpoints
            .Where(endpoint => !IsExempt(endpoint.RoutePattern.RawText)
                && string.IsNullOrWhiteSpace(PolicyOf(endpoint)))
            .Select(Describe)
            .ToList();
        if (undeclared.Count > 0)
        {
            throw new XunitException($"{Rule}; sem política: {string.Join("; ", undeclared)}");
        }
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        return $"{string.Join(",", methods)} {endpoint.RoutePattern.RawText ?? endpoint.DisplayName}".Trim();
    }
}
