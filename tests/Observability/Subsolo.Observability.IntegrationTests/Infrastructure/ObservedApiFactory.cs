using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Observability.IntegrationTests.Infrastructure;

/// <summary>
/// O Subsolo.Api em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) em Development — migrações e seed
/// (cidade <c>cidade-exemplo</c>) no start — com:
/// <list type="bullet">
/// <item>exportadores em memória registrados depois do ServiceDefaults (<see cref="Logs"/>, <see cref="Spans"/>: o que
/// o exportador OTLP receberia);</item>
/// <item>opcionalmente, o destino de exportação (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c> e a connection string
/// <c>seq</c>) apontando para <paramref name="otlpEndpoint"/>;</item>
/// <item>opcionalmente, os <see cref="IEndpoint"/> deste assembly de teste.</item>
/// </list>
/// </summary>
internal sealed class ObservedApiFactory(string connectionString, Uri? otlpEndpoint = null, bool withTestEndpoints = false)
    : WebApplicationFactory<Program>
{
    private readonly List<LogRecord> _logs = [];
    private readonly List<Activity> _spans = [];

    public IReadOnlyList<LogRecord> Logs
    {
        get
        {
            Services.GetRequiredService<LoggerProvider>().ForceFlush();
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
            Services.GetRequiredService<TracerProvider>().ForceFlush();
            lock (_spans)
            {
                return [.. _spans];
            }
        }
    }

    /// <summary>
    /// Os spans exportados, assim que algum satisfaz <paramref name="predicate"/> (ou o último retrato, ao fim de
    /// <paramref name="timeout"/>, para as asserções relatarem o que chegou). No TestServer o span do servidor pode
    /// terminar depois que o cliente já recebeu a resposta (ex.: caminho do <c>UseExceptionHandler</c>).
    /// </summary>
    public async Task<IReadOnlyList<Activity>> SpansWhenAsync(
        Func<Activity, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var spans = Spans;
            if (spans.Any(predicate) || watch.Elapsed >= timeout)
            {
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    /// <summary>Endpoint que emite o token anti-forgery (SPEC-0010).</summary>
    private const string AntiforgeryRoute = "/api/v1/antiforgery";

    /// <summary>Cookie <c>__Host-</c> (Secure) par do token enviado em <see cref="AntiforgeryFilter.HeaderName"/>.</summary>
    private const string AntiforgeryCookieName = "__Host-subsolo-csrf";

    /// <summary>
    /// Cliente HTTPS (o cookie <c>__Host-</c> do anti-forgery é Secure) sem contêiner de cookies: o teste monta o
    /// cabeçalho <c>Cookie</c> por conta própria, juntando o cookie do anti-forgery aos seus.
    /// </summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    /// <summary>
    /// <c>GET /api/v1/antiforgery</c>: o token (para o cabeçalho <see cref="AntiforgeryFilter.HeaderName"/>) e o par
    /// <c>nome=valor</c> do cookie anti-forgery (para o cabeçalho <c>Cookie</c>) que as mutações da SPEC-0010 exigem.
    /// </summary>
    public static async Task<(string Token, string Cookie)> AntiforgeryAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(AntiforgeryRoute, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = payload.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("GET /api/v1/antiforgery sem token");
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(value => value.Split(';', 2)[0])
                .FirstOrDefault(pair => pair.StartsWith($"{AntiforgeryCookieName}=", StringComparison.Ordinal))
            : null;
        return (token, cookie ?? throw new InvalidOperationException("GET /api/v1/antiforgery sem o cookie anti-forgery"));
    }

    /// <summary>Um database novo (vazio) no servidor de <paramref name="postgresConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string postgresConnectionString, CancellationToken cancellationToken)
    {
        var name = $"obs_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(postgresConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
#pragma warning disable CA2100 // nome gerado aqui (guid), sem entrada externa
            await using var create = new NpgsqlCommand($"create database \"{name}\"", connection);
#pragma warning restore CA2100
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(postgresConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Endereço local sem nenhum serviço escutando (o "Seq fora do ar").</summary>
    public static Uri DeadEndpoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return new Uri($"http://127.0.0.1:{port}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        if (otlpEndpoint is not null)
        {
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint.ToString());
            builder.UseSetting("ConnectionStrings:seq", otlpEndpoint.ToString());
        }

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(_logs));
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
            if (withTestEndpoints)
            {
                services.AddEndpoints(typeof(ObservedApiFactory).Assembly);
            }
        });
    }
}

/// <summary>Database isolado por teste no PostgreSQL compartilhado da coleção.</summary>
internal static class ObservedApi
{
    public static Task<string> DatabaseAsync(PostgresFixture postgres, CancellationToken cancellationToken) =>
        ObservedApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, cancellationToken);
}
