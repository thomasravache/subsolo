using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0010 — regra do ADR-0003 (autorização negada por padrão): todo endpoint declara
/// <c>RequireAuthorization(&lt;política&gt;)</c> ou <c>AllowAnonymous()</c> explicitamente.
/// </summary>
public static class EndpointAuthorizationRules
{
    /// <summary>Os endpoints que <paramref name="endpoints"/> mapeiam, num host mínimo (sem servidor).</summary>
    public static IReadOnlyList<RouteEndpoint> EndpointsOf(params IEndpoint[] endpoints) =>
        throw new NotImplementedException("SPEC-0010: scaffold — mapear os IEndpoint num host mínimo.");

    /// <summary>Os endpoints de todo assembly <c>Subsolo.Modules.*</c> ao lado do assembly de teste.</summary>
    public static IReadOnlyList<RouteEndpoint> ModuleEndpoints() =>
        throw new NotImplementedException("SPEC-0010: scaffold — mapear os IEndpoint de todos os módulos.");

    /// <summary>
    /// Falha (exceção) nomeando cada endpoint sem <c>RequireAuthorization</c> nem <c>AllowAnonymous</c> explícito.
    /// </summary>
    public static void Check(IEnumerable<RouteEndpoint> endpoints) =>
        throw new NotImplementedException("SPEC-0010: scaffold — verificar a declaração de autorização.");
}
