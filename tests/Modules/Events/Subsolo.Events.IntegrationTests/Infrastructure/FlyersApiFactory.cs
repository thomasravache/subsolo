using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0022 — o Subsolo.Api em memória em Development sobre um database próprio do teste (copiado do
/// <see cref="AdminApiFactory"/> da SPEC-0017 e do <c>ProfilesApiFactory</c> da SPEC-0021), com:
/// <list type="bullet">
/// <item><c>Media:Root</c> = <see cref="MediaRoot"/>, um diretório temporário só deste host (apagado no fim), dentro de
/// <see cref="Sandbox"/> (onde os testes de caminho com <c>..</c> deixam um arquivo que não pode vazar);</item>
/// <item><c>Users:AdminEmails</c> = [<see cref="AdminEmail"/>] e a entrada de teste <see cref="FlyersTestEndpoints.SignIn"/>;</item>
/// <item>os limites <c>default</c> e <c>report</c> altos, a menos que <paramref name="productionRateLimits"/>;</item>
/// <item>opcionalmente, o <see cref="TimeProvider"/> trocado por <paramref name="clock"/>;</item>
/// <item>SPEC-0023 — opcionalmente, configurações a mais (<paramref name="settings"/>, ex.: <c>Media:QueueTimeout</c>) e
/// serviços a mais (<paramref name="configureServices"/>, ex.: o dublê do limite de corpo do Kestrel).</item>
/// </list>
/// </summary>
internal sealed class FlyersApiFactory(
    string connectionString,
    FakeTimeProvider? clock = null,
    bool productionRateLimits = false,
    IReadOnlyDictionary<string, string>? settings = null,
    Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subsolo.test";

    private static readonly Dictionary<string, string> HighRateLimits = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "100000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
        ["Security:RateLimiting:report:PermitLimit"] = "100000",
        ["Security:RateLimiting:report:Window"] = "01:00:00",
    };

    /// <summary>Diretório temporário do teste; contém <see cref="MediaRoot"/>.</summary>
    public string Sandbox { get; } = CreateSandbox();

    /// <summary>O <c>Media:Root</c> deste host.</summary>
    public string MediaRoot => Path.Combine(Sandbox, "media");

    public string ConnectionString => connectionString;

    /// <summary>Cliente HTTPS (cookies <c>__Host-</c>) com pote de cookies próprio, sem seguir redirecionamentos.</summary>
    public FlyersApiClient CreateFlyersClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>Um cliente já logado com <paramref name="email"/> (e o id do usuário).</summary>
    public async Task<(FlyersApiClient Client, Guid UserId)> SignedInAsync(string email, CancellationToken ct)
    {
        var client = CreateFlyersClient();
        var userId = await client.SignInAsync(email, ct);
        return (client, userId);
    }

    /// <summary>Um cliente logado com o papel <c>artist</c> (concedido pelo evento do Users, como na aprovação de perfil).</summary>
    public async Task<(FlyersApiClient Client, Guid UserId)> ArtistAsync(string email, CancellationToken ct)
    {
        var (client, userId) = await SignedInAsync(email, ct);
        await client.BecomeArtistAsync(Services, userId, ct);
        return (client, userId);
    }

    /// <summary>Todos os arquivos (caminhos relativos a <see cref="MediaRoot"/>) gravados no <c>Media:Root</c>.</summary>
    public IReadOnlyList<string> MediaFiles() =>
        Directory.Exists(MediaRoot)
            ? [.. Directory.EnumerateFiles(MediaRoot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(MediaRoot, path))
                .Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"flyers_{Guid.NewGuid():N}";
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

    /// <summary>Para o host, devolve ao PostgreSQL as conexões ociosas do pool e apaga o diretório temporário.</summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        using (var connection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        try
        {
            Directory.Delete(Sandbox, recursive: true);
        }
        catch (IOException)
        {
            // Diretório temporário: o que sobrar o sistema limpa.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        // SPEC-0016 (Emenda 5): a API exige App:PublicUrl na inicialização.
        builder.UseSetting("App:PublicUrl", "https://localhost");
        builder.UseSetting("Users:AdminEmails:0", AdminEmail);
        builder.UseSetting("Media:Root", MediaRoot);
        foreach (var (key, value) in productionRateLimits ? [] : HighRateLimits)
        {
            builder.UseSetting(key, value);
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddEndpoints(typeof(FlyersApiFactory).Assembly);
            if (clock is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            }

            configureServices?.Invoke(services);
        });
    }

    private static string CreateSandbox()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"subsolo-spec0022-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(sandbox, "media"));
        return sandbox;
    }
}
