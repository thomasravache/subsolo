using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Diagnostics;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.Domain.Styles;
using Subsolo.Modules.Events.Infrastructure.Catalog;
using Subsolo.Modules.Events.Infrastructure.Cities;
using Subsolo.Modules.Events.Infrastructure.Flyers;
using Subsolo.Modules.Events.Infrastructure.Shows;
using Subsolo.Modules.Events.Infrastructure.Styles;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Events.Presentation;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — registro do módulo Events: <c>services.AddEventsModule(configuration)</c> no host.</summary>
public static class EventsModule
{
    /// <summary>SPEC-0022 — nome do <c>Meter</c> do módulo (<c>subsolo.flyers.uploads</c>, <c>subsolo.flyers.processing_duration</c>).</summary>
    public const string MeterName = EventsTelemetry.MeterName;

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
        // SPEC-0022: flyers (upload, catálogo interno para o cadastro de show e limpeza de hora em hora).
        services.AddScoped<IFlyerRepository, FlyerRepository>();
        services.AddScoped<IFlyerCatalog, FlyerCatalog>();
        // SPEC-0023: shows (cadastro, agenda, "Meus shows") e a seção "shows" do export de dados pessoais. Os perfis vêm
        // do IShowProfiles, implementado pelo módulo Profiles (Emenda v2).
        services.AddScoped<IShowRepository, ShowRepository>();
        services.AddScoped<IShowsReadModel, ShowsReadModel>();
        services.AddScoped<IPersonalDataSection, ShowsPersonalDataSection>();
        // O assembly do domínio entra para a outbox achar os eventos de domínio dos shows.
        services.AddModule<EventsDbContext>(
            configuration,
            typeof(EventsModule).Assembly,
            typeof(GetCityAgendaQuery).Assembly,
            typeof(Show).Assembly);
        services.ConfigureAllQuartzSchedulers(CleanupFlyersJob.Schedule);
        // SPEC-0025: data mal formada na agenda → 400 agenda.invalid_range (também com ThrowOnBadRequest).
        services.AddEventsPresentation();
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
