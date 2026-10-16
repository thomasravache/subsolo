using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
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
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "Subsolo.Observability.UnitTests",
            EnvironmentName = "Testing",
        });
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
}
