using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Modules.Template.Application;

namespace Subsolo.Modules.Template.Infrastructure;

/// <summary>Dublê de teste: registro do módulo Template no padrão <c>services.Add&lt;M&gt;Module(configuration)</c>.</summary>
public static class TemplateModule
{
    public static IServiceCollection AddTemplateModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TemplateFaults>();
        return services.AddModule<TemplateDbContext>(configuration, typeof(TemplateModule).Assembly);
    }
}
