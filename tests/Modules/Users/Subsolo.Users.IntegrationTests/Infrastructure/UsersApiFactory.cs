using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// O Subsolo.Api em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) — por padrão em Development, que aplica
/// as migrações no start — com o banco do teste e:
/// <list type="bullet">
/// <item><c>Users:AdminEmails</c> = [<see cref="AdminEmail"/>] e as configurações extras de <paramref name="settings"/>
/// (ex.: limites de rate limit);</item>
/// <item>os <see cref="UsersTestEndpoints"/> (entrada pelo <c>ISignInService</c> e endpoint <c>admin</c>);</item>
/// <item>o IP de origem tirado do cabeçalho <see cref="RemoteIpHeader"/> (o TestServer não tem IP);</item>
/// <item>opcionalmente, o <see cref="TimeProvider"/> trocado por <paramref name="clock"/>;</item>
/// <item>o <c>ISessionValidator</c> medido (<see cref="Timings"/>) e logs e spans exportados em memória
/// (<see cref="Logs"/>, <see cref="Spans"/>).</item>
/// </list>
/// </summary>
internal sealed class UsersApiFactory(
    string connectionString,
    string environment = "Development",
    FakeTimeProvider? clock = null,
    IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subsolo.test";

    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    /// <summary>IP de origem quando a requisição não manda <see cref="RemoteIpHeader"/>.</summary>
    public static readonly IPAddress DefaultRemoteIp = IPAddress.Parse("203.0.113.10");

    private readonly List<LogRecord> _logs = [];
    private readonly List<Activity> _spans = [];

    public SessionValidationTimings Timings { get; } = new();

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

    /// <summary>Cliente HTTPS (cookies <c>Secure</c>/<c>__Host-</c>) com o pote de cookies do <see cref="UsersApiClient"/>.</summary>
    public UsersApiClient CreateUsersClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>O ticket do cookie de sessão, aberto com a proteção de dados do host.</summary>
    public Microsoft.AspNetCore.Authentication.AuthenticationTicket? UnprotectSessionCookie(string cookieValue) =>
        Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme)
            .TicketDataFormat.Unprotect(cookieValue);

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"users_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(serverConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
#pragma warning disable CA2100 // nome gerado aqui (guid), sem entrada externa
            await using var create = new NpgsqlCommand($"create database \"{name}\"", connection);
#pragma warning restore CA2100
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>
    /// Para o host e devolve ao PostgreSQL as conexões ociosas do pool deste banco: cada teste usa um banco próprio, e
    /// os pools dos hosts já parados (até 5 min ociosos no Npgsql) esgotariam o <c>max_connections</c> do container.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        // SPEC-0016 (Emenda 5): a API valida App:PublicUrl na inicialização (https; http só em Development) e não tem
        // valor padrão no código; o host de teste roda em qualquer ambiente com o endereço do cliente de teste.
        builder.UseSetting("App:PublicUrl", "https://localhost");
        builder.UseSetting("Users:AdminEmails:0", AdminEmail);
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddEndpoints(typeof(UsersApiFactory).Assembly);
            services.AddTransient<IStartupFilter, RemoteIpStartupFilter>();
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(_logs));
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
            if (clock is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            }

            Timings.Decorate(services);
        });
    }

    /// <summary>Define <c>Connection.RemoteIpAddress</c> antes do pipeline da aplicação.</summary>
    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress =
                    context.Request.Headers.TryGetValue(RemoteIpHeader, out var ip) && IPAddress.TryParse(ip, out var parsed)
                        ? parsed
                        : DefaultRemoteIp;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
