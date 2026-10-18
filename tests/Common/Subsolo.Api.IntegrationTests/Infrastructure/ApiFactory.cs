using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;

namespace Subsolo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// O Subsolo.Api em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) no ambiente pedido, com a connection
/// string do banco apontando para o PostgreSQL do teste e, opcionalmente, os <see cref="IEndpoint"/> deste assembly
/// de teste registrados (o host mapeia todo <see cref="IEndpoint"/> registrado no contêiner).
/// </summary>
internal sealed class ApiFactory(string environment, string connectionString, bool withTestEndpoints = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting($"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}", connectionString);
        // SPEC-0016 (Emenda 5): a API valida App:PublicUrl na inicialização (https; http só em Development) e não tem
        // valor padrão no código; o host de teste roda em qualquer ambiente com o endereço do cliente de teste.
        builder.UseSetting("App:PublicUrl", "https://localhost");
        if (withTestEndpoints)
        {
            builder.ConfigureTestServices(services => services.AddEndpoints(typeof(ApiFactory).Assembly));
        }
    }
}
