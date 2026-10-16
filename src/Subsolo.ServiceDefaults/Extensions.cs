using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// SPEC-0007 — padrões de serviço do template do Aspire 13.6: OpenTelemetry (exportação OTLP), health checks,
/// resiliência de HttpClient e service discovery.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>Todas as <c>ActivitySource</c> próprias do Subsolo.</summary>
    private const string SubsoloSources = "Subsolo.*";

    /// <summary>
    /// Fonte das execuções de job do Quartz 4 (valor de <c>Quartz.Diagnostics.QuartzInstrumentation.ActivitySourceName</c>,
    /// repetido aqui para o ServiceDefaults não depender do Quartz).
    /// </summary>
    private const string QuartzSource = "Quartz";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                // Comandos, consultas e outbox/inbox (Subsolo.Application, Subsolo.Messaging).
                .AddSource(SubsoloSources)
                .AddSource(QuartzSource)
                .AddAspNetCoreInstrumentation(aspNetCore =>
                    // Os health checks não poluem os traces.
                    aspNetCore.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                        !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Verificação de vida: o processo responde.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>Mapeia <c>/health</c> (pronto) e <c>/alive</c> (vivo).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Todos os checks precisam passar para a aplicação estar pronta para receber tráfego.
        // Públicos (SPEC-0010): a política de fallback da API exige usuário autenticado.
        app.MapHealthChecks(HealthEndpointPath).AllowAnonymous();

        // Só os checks com a tag "live" precisam passar para a aplicação ser considerada viva.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
        }).AllowAnonymous();

        return app;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // O AppHost injeta OTEL_EXPORTER_OTLP_ENDPOINT (dashboard do Aspire); sem ele, nada é exportado.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
