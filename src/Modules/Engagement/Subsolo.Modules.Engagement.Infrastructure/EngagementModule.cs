using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>SPEC-0027 — registro do módulo Engagement: <c>services.AddEngagementModule(configuration)</c> no host.</summary>
public static class EngagementModule
{
    /// <summary>
    /// Scaffold do contrato: a interface pública do módulo existe no contêiner, mas ainda não está implementada (a
    /// implementação chama <c>AddModule&lt;EngagementDbContext&gt;</c> e <c>AddEndpoints</c>, como o módulo Profiles).
    /// </summary>
    public static IServiceCollection AddEngagementModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IEngagementApi>(_ =>
            throw new NotImplementedException("SPEC-0027: IEngagementApi ainda não implementado."));
        return services;
    }
}
