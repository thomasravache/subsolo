using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// Um host com <c>AddServiceDefaults()</c> (o mesmo pipeline de telemetria da API) e exportadores em memória
/// registrados depois dele: o que chega a <see cref="Logs"/> e <see cref="Spans"/> é o que um exportador OTLP
/// receberia (já passado pelos processadores do ServiceDefaults). Sem I/O.
/// </summary>
internal sealed class ServiceDefaultsTelemetry : IDisposable
{
    private readonly IHost _host;
    private readonly List<LogRecord> _logs = [];
    private readonly List<Activity> _spans = [];

    public ServiceDefaultsTelemetry(params string[] extraSources)
        : this("Testing", new Dictionary<string, string?>(), extraSources)
    {
    }

    /// <summary>
    /// Host no ambiente <paramref name="environmentName"/> com a configuração <paramref name="configuration"/> (ex.:
    /// <c>OTEL_SERVICE_NAME</c>, que o AppHost injeta com o nome do recurso).
    /// </summary>
    public ServiceDefaultsTelemetry(
        string environmentName,
        IReadOnlyDictionary<string, string?> configuration,
        params string[] extraSources)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "Subsolo.Observability.UnitTests",
            EnvironmentName = environmentName,
        });
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.AddServiceDefaults();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(_logs));
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
        {
            foreach (var source in extraSources)
            {
                tracing.AddSource(source);
            }

            tracing.AddInMemoryExporter(_spans);
        });

        _host = builder.Build();

        // Os provedores do OpenTelemetry são criados sob demanda: resolvê-los liga os listeners agora.
        _ = _host.Services.GetRequiredService<TracerProvider>();
        _ = _host.Services.GetRequiredService<ILoggerFactory>();
    }

    public IServiceProvider Services => _host.Services;

    /// <summary>Atributos do resource dos logs (o que o exportador OTLP envia em <c>resource</c>).</summary>
    public IReadOnlyDictionary<string, string?> LogResource =>
        ResourceAttributes(_host.Services.GetRequiredService<LoggerProvider>().GetResource());

    /// <summary>Atributos do resource dos spans.</summary>
    public IReadOnlyDictionary<string, string?> SpanResource =>
        ResourceAttributes(_host.Services.GetRequiredService<TracerProvider>().GetResource());

    public ILogger CreateLogger(string category) =>
        _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

    public IReadOnlyList<LogRecord> Logs
    {
        get
        {
            _host.Services.GetRequiredService<LoggerProvider>().ForceFlush();
            lock (_logs)
            {
                return [.. _logs];
            }
        }
    }

    public IReadOnlyList<Activity> Spans
    {
        get
        {
            _host.Services.GetRequiredService<TracerProvider>().ForceFlush();
            lock (_spans)
            {
                return [.. _spans];
            }
        }
    }

    public void Dispose() => _host.Dispose();

    private static Dictionary<string, string?> ResourceAttributes(Resource resource) =>
        resource.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value?.ToString(), StringComparer.Ordinal);
}
