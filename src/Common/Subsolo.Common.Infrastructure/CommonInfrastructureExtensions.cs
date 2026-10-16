using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Infrastructure.EventBus;
using Subsolo.Common.Infrastructure.Outbox;

namespace Subsolo.Common.Infrastructure;

/// <summary>SPEC-0007 — serviços compartilhados por todos os módulos, registrados uma vez pelo host.</summary>
public static class CommonInfrastructureExtensions
{
    /// <summary>Nome da connection string do banco da aplicação (database <c>subsolo</c> do AppHost).</summary>
    public const string ConnectionStringName = "subsolo";

    /// <summary>
    /// Registra o <c>IEventBus</c> em processo, o Quartz (hosted service, no <see cref="TimeProvider"/> do
    /// contêiner), o <see cref="TimeProvider"/> do sistema (substituível nos testes) e o interceptor da outbox. A
    /// connection string <see cref="ConnectionStringName"/> é lida de <paramref name="configuration"/> quando o
    /// DbContext de um módulo é criado.
    /// </summary>
    public static IServiceCollection AddCommonInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InsertOutboxMessagesInterceptor>();
        services.TryAddSingleton<IEventBus, InProcessEventBus>();

        services.AddQuartz(_ => { });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }

    /// <summary>Connection string do banco da aplicação, ou erro claro quando não está configurada.</summary>
    internal static string ConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException(
            $"Connection string \"{ConnectionStringName}\" não configurada (ConnectionStrings:{ConnectionStringName}).");
}
