using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0017 — o Subsolo.Api em memória em Development (aplica as migrações — com os estilos iniciais — e o seed das
/// cidades de desenvolvimento no start) sobre um database próprio do teste, com <c>Users:AdminEmails</c> =
/// [<see cref="AdminEmail"/>] e a entrada de teste <see cref="AdminTestEndpoints.SignIn"/> (pelo <c>ISignInService</c>
/// do host, como no <c>UsersApiFactory</c> da SPEC-0014). Um database por teste: o <c>ResetAsync</c> do
/// <c>PostgresFixture</c> apagaria os estilos que a migração grava.
/// </summary>
internal sealed class AdminApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@subsolo.test";

    public const string FanEmail = "fa@subsolo.test";

    /// <summary>Cliente HTTPS (cookies <c>__Host-</c>) com pote de cookies próprio, sem seguir redirecionamentos.</summary>
    public AdminApiClient CreateAdminClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        }));

    /// <summary>Um database novo (vazio) no servidor de <paramref name="serverConnectionString"/>.</summary>
    public static async Task<string> IsolatedDatabaseAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = $"events_{Guid.NewGuid():N}";
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
        builder.ConfigureTestServices(services => services.AddEndpoints(typeof(AdminApiFactory).Assembly));
    }
}
