using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Modules.TemplateB.Infrastructure;

/// <summary>Dublê de teste: registro do módulo TemplateB no padrão <c>services.Add&lt;M&gt;Module(configuration)</c>.</summary>
public static class TemplateBModule
{
    public static IServiceCollection AddTemplateBModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TemplateBProbe>();
        return services.AddModule<TemplateBDbContext>(configuration, typeof(TemplateBModule).Assembly);
    }
}
