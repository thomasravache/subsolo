using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — registro do módulo Events: <c>services.AddEventsModule(configuration)</c> no host.</summary>
public static class EventsModule
{
    /// <summary>Chama <c>AddModule&lt;EventsDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo.</summary>
    public static IServiceCollection AddEventsModule(this IServiceCollection services, IConfiguration configuration) =>
        throw new NotImplementedException();
}
