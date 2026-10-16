using Microsoft.AspNetCore.Builder;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// SPEC-0007 — padrões de serviço do template do Aspire 13.6: OpenTelemetry (exportação OTLP), health checks,
/// resiliência de HttpClient e service discovery.
/// </summary>
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        throw new NotImplementedException();

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        throw new NotImplementedException();

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        throw new NotImplementedException();

    /// <summary>Mapeia <c>/health</c> (pronto) e <c>/alive</c> (vivo).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app) => throw new NotImplementedException();
}
