using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Site.Application;
using Subsolo.Modules.Site.Domain.Supporters;
using Subsolo.Modules.Site.Infrastructure.Supporters;
using Subsolo.Modules.Site.Presentation;

namespace Subsolo.Modules.Site.Infrastructure;

/// <summary>SPEC-0028 — registro do módulo Site: <c>services.AddSiteModule(configuration)</c> no host.</summary>
public static class SiteModule
{
    /// <summary>
    /// Chama <c>AddModule&lt;SiteDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra o
    /// repositório de apoiadores e a unidade de trabalho.
    /// </summary>
    public static IServiceCollection AddSiteModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ISupporterRepository, SupporterRepository>();
        services.AddScoped<ISiteUnitOfWork>(provider => provider.GetRequiredService<SiteDbContext>());

        services.AddModule<SiteDbContext>(
            configuration,
            typeof(SiteModule).Assembly,
            typeof(SiteErrors).Assembly,
            typeof(Supporter).Assembly);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
