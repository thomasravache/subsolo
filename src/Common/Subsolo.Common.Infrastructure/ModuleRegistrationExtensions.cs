using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure.Database;

namespace Subsolo.Common.Infrastructure;

/// <summary>SPEC-0007 — peças comuns do registro de um módulo, usadas pelo <c>Add&lt;M&gt;Module</c> de cada módulo.</summary>
public static class ModuleRegistrationExtensions
{
    /// <summary>
    /// Registra o <typeparamref name="TDbContext"/> do módulo (PostgreSQL, connection string
    /// <see cref="CommonInfrastructureExtensions.ConnectionStringName"/>, histórico de migrações no schema do módulo),
    /// varre <paramref name="assemblies"/> (Scrutor) atrás de handlers de comando/consulta, validadores,
    /// <c>IDomainEventHandler</c>, <c>IIntegrationEventHandler</c> e <c>IEndpoint</c>, aplica os decorators de
    /// validação e log, inscreve os handlers de integração na inbox do módulo e agenda os jobs
    /// <c>ProcessOutbox</c>/<c>ProcessInbox</c> do módulo.
    /// </summary>
    public static IServiceCollection AddModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
        where TDbContext : ModuleDbContext =>
        throw new NotImplementedException();
}
