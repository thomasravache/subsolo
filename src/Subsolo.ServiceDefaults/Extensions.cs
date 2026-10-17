using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Subsolo.ServiceDefaults;
using Subsolo.ServiceDefaults.Redaction;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// SPEC-0007 — padrões de serviço do template do Aspire 13.6: OpenTelemetry (exportação OTLP), health checks,
/// resiliência de HttpClient e service discovery.
/// SPEC-0009 — mascaramento de dados pessoais em todo log (OTLP e console) e em todo span exportado (ADR-0008),
/// spans do PostgreSQL (Npgsql) e <c>/health</c> em JSON <c>{status, entries}</c>.
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

    /// <summary>Atributo do resource com o ambiente (contrato da SPEC-0009).</summary>
    private const string DeploymentEnvironmentAttribute = "deployment.environment";

    /// <summary>Fonte das consultas do Npgsql (o que o pacote <c>Npgsql.OpenTelemetry</c> assina).</summary>
    private const string NpgsqlSource = "Npgsql";

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

            // Antes dos exportadores: o nome do evento como atributo consultável no Seq.
            logging.AddProcessor(new EventNameLogProcessor());
        });

        // Todo log passa pelo redator antes de chegar a qualquer provedor (OTLP, console).
        builder.Services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(services => new RedactingLoggerFactory(
            new LoggerFactory(
                services.GetServices<ILoggerProvider>(),
                services.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>(),
                services.GetService<IOptions<LoggerFactoryOptions>>(),
                services.GetService<IExternalScopeProvider>()))));

        builder.Services.AddOpenTelemetry()
            // Resource de logs, spans e métricas: service.name (OTEL_SERVICE_NAME) e o ambiente do host.
            .ConfigureResource(resource => resource.AddAttributes(
            [
                new KeyValuePair<string, object>(DeploymentEnvironmentAttribute, builder.Environment.EnvironmentName),
            ]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                // Comandos, consultas e outbox/inbox (Subsolo.Application, Subsolo.Messaging).
                .AddSource(SubsoloSources)
                .AddSource(QuartzSource)
                .AddSource(NpgsqlSource)
                .AddAspNetCoreInstrumentation(aspNetCore =>
                    // Os health checks não poluem os traces.
                    aspNetCore.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                        !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation()
                // Antes dos exportadores: nenhum span sai sem o mascaramento.
                .AddProcessor<RedactingActivityProcessor>());

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
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions { ResponseWriter = WriteHealthAsync })
            .AllowAnonymous();

        // Só os checks com a tag "live" precisam passar para a aplicação ser considerada viva.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Corpo do <c>/health</c>: <c>{"status": "Healthy|Degraded|Unhealthy", "entries": {"nome": {"status",
    /// "description"}}}</c> — sem a mensagem das exceções (pode trazer detalhes da infraestrutura).
    /// </summary>
    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Exception is null ? entry.Value.Description : null,
                },
                StringComparer.Ordinal),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, body, cancellationToken: context.RequestAborted);
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
