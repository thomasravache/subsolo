using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Modules.Site.Infrastructure;

/// <summary>SPEC-0028 — registro do módulo Site: <c>services.AddSiteModule(configuration)</c> no host.</summary>
public static class SiteModule
{
    /// <summary>
    /// Scaffold do contrato: ainda não registra nada (a implementação chama <c>AddModule&lt;SiteDbContext&gt;</c> e
    /// <c>AddEndpoints</c>, como o módulo Engagement).
    /// </summary>
    public static IServiceCollection AddSiteModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
