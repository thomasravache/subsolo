using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Subsolo.Common.Infrastructure;
using Subsolo.Modules.Template.Infrastructure;

namespace Subsolo.Common.UnitTests.Infrastructure;

/// <summary>
/// Contêiner com o módulo de exemplo Template registrado como o host registra os módulos
/// (<c>AddCommonInfrastructure</c> + <c>AddTemplateModule</c>, que chama <c>AddModule</c>), com logs falsos. Nenhum
/// serviço que acessa o banco é resolvido: a connection string só existe para o registro.
/// </summary>
internal static class TemplateModuleServices
{
    public static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}"] = "Host=localhost;Database=unused",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddCommonInfrastructure(configuration);
        services.AddTemplateModule(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Mensagens e estado estruturado de todos os registros de log coletados.</summary>
    public static string LoggedText(IServiceProvider services) =>
        string.Join('\n', services.GetFakeLogCollector().GetSnapshot().SelectMany(record =>
            new[] { record.Message }.Concat(record.StructuredState?.Select(pair => $"{pair.Key}={pair.Value}") ?? [])));
}
