using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Profiles.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0021 — o Subsolo.Api em memória em Development (aplica as migrações — com os estilos iniciais — e o seed das
/// cidades de desenvolvimento <c>cidade-exemplo</c> e <c>outra-cidade</c> no start) sobre um database próprio do teste
/// (copiado do <c>AdminApiFactory</c> da SPEC-0017 e do <c>UsersApiFactory</c> da SPEC-0014), com:
/// <list type="bullet">
/// <item><c>Users:AdminEmails</c> = [<see cref="AdminEmail"/>] e a entrada de teste <see cref="ProfilesTestEndpoints.SignIn"/>;</item>
/// <item>os limites <c>default</c> e <c>report</c> altos (a menos que <paramref name="productionRateLimits"/>), para os
/// testes de volume, e as configurações extras de <paramref name="settings"/>;</item>
/// <item>opcionalmente, o <see cref="TimeProvider"/> trocado por <paramref name="clock"/>;</item>
/// <item>logs e spans exportados em memória (<see cref="Logs"/>, <see cref="Spans"/>).</item>
/// </list>
/// </summary>
internal sealed class ProfilesApiFactory(
    string connectionString,
    FakeTimeProvider? clock = null,
    IReadOnlyDictionary<string, string>? settings = null,
    bool productionRateLimits = false)
    : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subsolo.test";

    /// <summary>Cidade semeada em Development (SPEC-0008).</summary>
    public const string City = "cidade-exemplo";

    /// <summary>Estilo semeado pela migração do Events (SPEC-0017).</summary>
    public const string Style = "death";

    private static readonly Dictionary<string, string> HighRateLimits = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "100000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
        ["Security:RateLimiting:report:PermitLimit"] = "100000",
        ["Security:RateLimiting:report:Window"] = "01:00:00",
    };

    private readonly List<LogRecord> _logs = [];
    private readonly List<Activity> _spans = [];

    public string ConnectionString => connectionString;

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

    /// <summary>Cliente HTTPS (cookies <c>__Host-</c>) com pote de cookies próprio, sem seguir redirecionamentos.</summary>
    public ProfilesApiClient CreateProfilesClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>Um cliente já logado com <paramref name="email"/> (e o id do usuário).</summary>
    public async Task<(ProfilesApiClient Client, Guid UserId)> SignedInAsync(string email, CancellationToken ct)
    {
        var client = CreateProfilesClient();
        var userId = await client.SignInAsync(email, ct);
        return (client, userId);
    }

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"profiles_{Guid.NewGuid():N}";
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

    /// <summary>Para o host e devolve ao PostgreSQL as conexões ociosas do pool deste banco.</summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        // SPEC-0016 (Emenda 5): a API exige App:PublicUrl na inicialização.
        builder.UseSetting("App:PublicUrl", "https://localhost");
        builder.UseSetting("Users:AdminEmails:0", AdminEmail);
        foreach (var (key, value) in productionRateLimits ? [] : HighRateLimits)
        {
            builder.UseSetting(key, value);
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddEndpoints(typeof(ProfilesApiFactory).Assembly);
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(_logs));
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
            if (clock is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            }
        });
    }
}
