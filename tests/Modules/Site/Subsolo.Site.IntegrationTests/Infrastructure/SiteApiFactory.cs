using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Site.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0028 — o Subsolo.Api em memória em Development (aplica as migrações no start, com o schema <c>site</c>) sobre um
/// database próprio do teste (copiado do <c>EngagementApiFactory</c> da SPEC-0027), com <c>Users:AdminEmails</c> =
/// [<see cref="AdminEmail"/>], a entrada de teste <see cref="SiteTestEndpoints.SignIn"/> e o limite <c>default</c> alto
/// (as medidas de desempenho repetem a leitura pública).
/// </summary>
internal sealed class SiteApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subsolo.test";

    public string ConnectionString => connectionString;

    /// <summary>Cliente HTTPS (cookies <c>__Host-</c>) com pote de cookies próprio, sem seguir redirecionamentos.</summary>
    public SiteApiClient CreateSiteClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>Um cliente já logado com <paramref name="email"/>.</summary>
    public async Task<SiteApiClient> SignedInAsync(string email, CancellationToken ct)
    {
        var client = CreateSiteClient();
        await client.SignInAsync(email, ct);
        return client;
    }

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"site_{Guid.NewGuid():N}";
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
        builder.UseSetting("Security:RateLimiting:default:PermitLimit", "100000");
        builder.UseSetting("Security:RateLimiting:default:Window", "00:01:00");
        builder.ConfigureTestServices(services => services.AddEndpoints(typeof(SiteApiFactory).Assembly));
    }
}
