using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Xunit.Sdk;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0014 — regra de rate limit (ADR-0003): todo endpoint da API declara uma política de rate limiting
/// (<c>RequireRateLimiting(&lt;política&gt;)</c>), exceto <c>/health</c>, chamado pelo monitor e pelo orquestrador.
/// Os endpoints vêm de <see cref="EndpointAuthorizationRules.EndpointsOf"/> e
/// <see cref="EndpointAuthorizationRules.ModuleEndpoints"/>.
/// </summary>
public static class EndpointRateLimitRules
{
    /// <summary>Texto da regra, citado na falha.</summary>
    public const string Rule =
        "ADR-0003: todo endpoint da API declara uma política de rate limit (RequireRateLimiting), exceto /health";

    /// <summary>Rotas dispensadas da regra.</summary>
    public static readonly IReadOnlySet<string> Exempt = new HashSet<string>(StringComparer.Ordinal) { "/health" };

    /// <summary>A política de rate limit declarada no endpoint, ou <see langword="null"/>.</summary>
    public static string? PolicyOf(Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null
            ? endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            : null;
    }

    /// <summary>Falha (exceção) nomeando cada endpoint, fora de <see cref="Exempt"/>, sem política de rate limit.</summary>
    public static void Check(IEnumerable<RouteEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var undeclared = endpoints
            .Where(endpoint => !Exempt.Contains(endpoint.RoutePattern.RawText ?? string.Empty)
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
