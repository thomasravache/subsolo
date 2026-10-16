using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Common.Infrastructure;

/// <summary>SPEC-0007 — serviços compartilhados por todos os módulos, registrados uma vez pelo host.</summary>
public static class CommonInfrastructureExtensions
{
    /// <summary>Nome da connection string do banco da aplicação (database <c>subsolo</c> do AppHost).</summary>
    public const string ConnectionStringName = "subsolo";

    /// <summary>
    /// Registra o <c>IEventBus</c> em processo, o Quartz (hosted service), o <see cref="TimeProvider"/> do sistema
    /// (substituível nos testes) e o <c>IFileStorage</c>.
    /// </summary>
    public static IServiceCollection AddCommonInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        throw new NotImplementedException();
}
