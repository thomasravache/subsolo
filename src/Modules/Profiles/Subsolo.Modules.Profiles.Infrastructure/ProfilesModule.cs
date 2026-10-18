using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — registro do módulo Profiles: <c>services.AddProfilesModule(configuration)</c> no host.</summary>
public static class ProfilesModule
{
    /// <summary>
    /// Scaffold do contrato: a interface pública do módulo existe no contêiner, mas ainda não está implementada (a
    /// implementação chama <c>AddModule&lt;ProfilesDbContext&gt;</c> e <c>AddEndpoints</c>, como o módulo Users).
    /// </summary>
    public static IServiceCollection AddProfilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IProfilesApi>(_ =>
            throw new NotImplementedException("SPEC-0021: IProfilesApi ainda não implementado."));
        return services;
    }
}
