using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// O Subsolo.Api em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) em Development — que aplica as
/// migrações e o seed de desenvolvimento (cidade <c>cidade-exemplo</c>) no start — com a connection string
/// <c>subsolo</c> apontando para o PostgreSQL do teste.
/// </summary>
internal sealed class EventsApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:subsolo", connectionString);
    }
}
