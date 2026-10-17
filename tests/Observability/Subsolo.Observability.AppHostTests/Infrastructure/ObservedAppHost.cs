using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Subsolo.Observability.AppHostTests.Infrastructure;

/// <summary>
/// O <c>Subsolo.AppHost</c> (Aspire.Hosting.Testing, PostgreSQL efêmero) com a exportação OTLP da <c>api</c> e do
/// <c>web</c> redirecionada para um <see cref="OtlpTestCollector"/> pelas variáveis padrão do OpenTelemetry
/// (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, <c>OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</c>), definidas depois do modelo
/// do AppHost (prevalecem sobre as dele). Compartilhado pelos testes da classe (<c>IClassFixture</c>).
/// </summary>
public sealed class ObservedAppHost : IAsyncLifetime
{
    /// <summary>Folga para baixar imagens e instalar as dependências do front na primeira execução.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private OtlpTestCollector? _collector;
    private DistributedApplication? _app;

    internal OtlpTestCollector Collector => _collector ?? throw new InvalidOperationException("fixture não iniciado");

    public DistributedApplication App => _app ?? throw new InvalidOperationException("fixture não iniciado");

    /// <summary>Um <c>traceparent</c> W3C novo (amostrado) e o seu trace id.</summary>
    public static (string TraceParent, string TraceId) NewTraceParent()
    {
        var traceId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var spanId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        return ($"00-{traceId}-{spanId}-01", traceId);
    }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _collector = await OtlpTestCollector.StartAsync(ct);

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        foreach (var name in new[] { "api", "web" })
        {
            appHost.CreateResourceBuilder<IResourceWithEnvironment>(name)
                .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", _collector.Endpoint.ToString().TrimEnd('/'))
                .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
                .WithEnvironment("OTEL_BSP_SCHEDULE_DELAY", "1000")
                .WithEnvironment("OTEL_BLRP_SCHEDULE_DELAY", "1000");
        }

        _app = await appHost.BuildAsync(ct).WaitAsync(StartupTimeout, ct);
        await _app.StartAsync(ct).WaitAsync(StartupTimeout, ct);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct).WaitAsync(StartupTimeout, ct);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web", ct).WaitAsync(StartupTimeout, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_collector is not null)
        {
            await _collector.DisposeAsync();
        }
    }

    /// <summary>
    /// Espera (até <paramref name="timeout"/>) os spans do trace <paramref name="traceId"/> satisfazerem
    /// <paramref name="complete"/>; devolve os spans recebidos até então (completos ou não).
    /// </summary>
    internal async Task<IReadOnlyList<CollectedSpan>> WaitForTraceAsync(
        string traceId, Func<IReadOnlyList<CollectedSpan>, bool> complete, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var spans = Collector.Trace(traceId);
            if (complete(spans) || DateTimeOffset.UtcNow >= deadline)
            {
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }
}
