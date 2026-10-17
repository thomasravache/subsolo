using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Infrastructure;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// O módulo Notifications num host genérico composto como na API: <c>AddServiceDefaults</c> (OpenTelemetry com o
/// mascaramento de dados pessoais em logs e spans, fontes <c>Subsolo.*</c>) e <c>AddNotificationsModule</c>, com
/// <c>Notifications:Smtp</c> apontando para <c>smtpHost:smtpPort</c> sem TLS (como o Mailpit no local e no CI) e o
/// remetente <see cref="FromAddress"/> (o nome fica no padrão do contrato, "Subsolo"). Logs e spans exportados ficam
/// em memória (<see cref="Logs"/>, <see cref="Spans"/>). Sem configuração de ambiente nem appsettings
/// (<c>DisableDefaults</c>): nada sai para um coletor OTLP de verdade.
/// </summary>
internal sealed class NotificationsHost : IAsyncDisposable
{
    public const string FromAddress = "nao-responda@subsolo.test";

    private readonly IHost _host;
    private readonly List<LogRecord> _logs;
    private readonly List<Activity> _spans;

    private NotificationsHost(IHost host, List<LogRecord> logs, List<Activity> spans)
    {
        _host = host;
        _logs = logs;
        _spans = spans;
    }

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

    public static async Task<NotificationsHost> StartAsync(
        string smtpHost, int smtpPort, CancellationToken cancellationToken, string environment = "Development")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ApplicationName = "Subsolo.Api",
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Notifications:Smtp:Host"] = smtpHost,
            ["Notifications:Smtp:Port"] = smtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Notifications:Smtp:Security"] = "None",
            ["Notifications:Smtp:FromAddress"] = FromAddress,
        });

        var logs = new List<LogRecord>();
        var spans = new List<Activity>();
        builder.AddServiceDefaults();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(logs));
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans));
        builder.Services.AddNotificationsModule(builder.Configuration);

        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return new NotificationsHost(host, logs, spans);
    }

    /// <summary>Pedido do modelo <see cref="EmailTemplate.MagicLink"/>.</summary>
    public static EmailRequest MagicLink(string to, string link, string minutes = "15") =>
        new(to, EmailTemplate.MagicLink, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["link"] = link,
            ["minutes"] = minutes,
        });

    /// <summary><see cref="INotificationsApi.SendEmailAsync"/> num escopo novo, como uma requisição da API.</summary>
    public async Task<Result> SendAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var api = scope.ServiceProvider.GetRequiredService<INotificationsApi>();
        return await api.SendEmailAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _host.Dispose();
        }
    }
}
