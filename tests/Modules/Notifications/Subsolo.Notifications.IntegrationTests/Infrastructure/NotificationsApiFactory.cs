using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Subsolo.Common.Domain;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Notifications.Infrastructure.Unsubscribe;
using Subsolo.Modules.Notifications.Infrastructure.WebPush;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0030 — o Subsolo.Api em memória em Development (aplica as migrações no start) sobre um database próprio do teste
/// (copiado do <c>EngagementApiFactory</c> da SPEC-0027), com:
/// <list type="bullet">
/// <item><c>Notifications:Smtp</c> no Mailpit do teste (sem TLS) e <c>App:PublicUrl</c> = <see cref="PublicUrl"/> (base do
/// link de descadastro);</item>
/// <item>um par de chaves VAPID gerado no teste (<see cref="Vapid"/>) em <c>Notifications:WebPush</c> e uma chave de
/// descadastro aleatória (<see cref="UnsubscribeKey"/>) em <c>Notifications:Unsubscribe</c>: nenhum segredo real;</item>
/// <item>o servidor de push falso (<see cref="Push"/>) no lugar do handler do <c>HttpClient</c>
/// <see cref="WebPushOptions.HttpClientName"/>: nenhuma chamada sai para FCM, Mozilla ou Apple;</item>
/// <item>os limites <c>default</c> altos e a entrada de teste <see cref="NotificationsTestEndpoints.SignIn"/>;</item>
/// <item>trocas de serviço do próprio teste (<paramref name="configureServices"/>, por último), como os canais e o
/// diretório de destinatários falsos do IT-08;</item>
/// <item>SPEC-0031 — os logs exportados em memória (<see cref="Logs"/>).</item>
/// </list>
/// </summary>
internal sealed class NotificationsApiFactory(
    string connectionString,
    MailpitServer mailpit,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    public const string PublicUrl = "https://localhost";

    public const string VapidSubject = "mailto:avisos@subsolo.test";

    private static readonly Dictionary<string, string> HighRateLimits = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "100000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
        ["Security:RateLimiting:auth:PermitLimit"] = "100000",
        ["Security:RateLimiting:auth:Window"] = "00:01:00",
    };

    private readonly List<LogRecord> _logs = [];

    public string ConnectionString => connectionString;

    /// <summary>SPEC-0031 — os logs do host (OpenTelemetry, em memória).</summary>
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

    /// <summary>Par de chaves VAPID deste host (gerado no teste).</summary>
    public VapidKeys Vapid { get; } = VapidKeys.Generate();

    /// <summary>Chave do token de descadastro deste host (aleatória).</summary>
    public string UnsubscribeKey { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    /// <summary>O servidor de push falso que recebe toda chamada do canal Web Push.</summary>
    public FakePushService Push { get; } = new();

    /// <summary>Cliente HTTPS (cookies <c>__Host-</c>) com pote de cookies próprio, sem seguir redirecionamentos.</summary>
    public NotificationsApiClient CreateApiClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>Um cliente já logado com <paramref name="email"/> (e o id do usuário).</summary>
    public async Task<(NotificationsApiClient Client, Guid UserId)> SignedInAsync(string email, CancellationToken ct)
    {
        var client = CreateApiClient();
        var userId = await client.SignInAsync(email, ct);
        return (client, userId);
    }

    /// <summary><see cref="INotificationsApi.EnqueueAsync"/> num escopo novo, como faria outro módulo.</summary>
    public async Task<Result> EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationsApi>().EnqueueAsync(userIds, notice, ct);
    }

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"notifications_{Guid.NewGuid():N}";
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
        using (var connection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        builder.UseSetting("App:PublicUrl", PublicUrl);
        builder.UseSetting("Notifications:Smtp:Host", mailpit.SmtpHost);
        builder.UseSetting("Notifications:Smtp:Port", mailpit.SmtpPort.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Notifications:Smtp:Security", "None");
        builder.UseSetting("Notifications:Smtp:FromAddress", "nao-responda@subsolo.test");
        builder.UseSetting($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.PublicKey)}", Vapid.PublicKey);
        builder.UseSetting($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.PrivateKey)}", Vapid.PrivateKey);
        builder.UseSetting($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.Subject)}", VapidSubject);
        builder.UseSetting($"{UnsubscribeOptions.SectionName}:{nameof(UnsubscribeOptions.SigningKey)}", UnsubscribeKey);
        foreach (var (key, value) in HighRateLimits)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddEndpoints(typeof(NotificationsApiFactory).Assembly);
            services.AddHttpClient(WebPushOptions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Push);
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(_logs));
            configureServices?.Invoke(services);
        });
    }
}
