using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Styles;
using Subsolo.Modules.Events.Infrastructure.Catalog;
using Subsolo.Modules.Events.Infrastructure.Cities;
using Subsolo.Modules.Events.Infrastructure.Styles;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Events.Presentation;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — registro do módulo Events: <c>services.AddEventsModule(configuration)</c> no host.</summary>
public static class EventsModule
{
    /// <summary>Chama <c>AddModule&lt;EventsDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo.</summary>
    public static IServiceCollection AddEventsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CityRepository>();
        services.AddScoped<ICityRepository>(provider => provider.GetRequiredService<CityRepository>());
        services.AddScoped<ICityAdminRepository>(provider => provider.GetRequiredService<CityRepository>());
        services.AddScoped<IStyleRepository, StyleRepository>();
        // SPEC-0021: cidades e estilos para os outros módulos (Profiles).
        services.AddScoped<IEventsCatalog, EventsCatalog>();
        services.AddScoped<IEventsUnitOfWork>(provider => provider.GetRequiredService<EventsDbContext>());
        services.AddModule<EventsDbContext>(
            configuration,
            typeof(EventsModule).Assembly,
            typeof(GetCityAgendaQuery).Assembly);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
