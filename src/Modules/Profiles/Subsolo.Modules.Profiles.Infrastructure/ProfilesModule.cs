using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Diagnostics;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;
using Subsolo.Modules.Profiles.Infrastructure.Profiles;
using Subsolo.Modules.Profiles.Infrastructure.Requests;
using Subsolo.Modules.Profiles.IntegrationEvents;
using Subsolo.Modules.Profiles.Presentation;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — registro do módulo Profiles: <c>services.AddProfilesModule(configuration)</c> no host.</summary>
public static class ProfilesModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.profiles.requests</c>, <c>subsolo.profiles.decisions</c>).</summary>
    public const string MeterName = ProfilesTelemetry.MeterName;

    /// <summary>
    /// Chama <c>AddModule&lt;ProfilesDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra os
    /// repositórios, a unidade de trabalho, as leituras, a interface pública <see cref="IProfilesApi"/>, a seção
    /// <c>profiles</c> do export (<see cref="IPersonalDataSection"/>) e a limpeza periódica dos pedidos decididos.
    /// </summary>
    public static IServiceCollection AddProfilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IProfileRequestRepository, ProfileRequestRepository>();
        services.AddScoped<IProfilesUnitOfWork>(provider => provider.GetRequiredService<ProfilesDbContext>());
        services.AddScoped<IProfilesReadModel, ProfilesReadModel>();
        services.AddScoped<IProfilesMaintenance, ProfilesMaintenance>();
        services.AddScoped<IProfilesApi, ProfilesApi>();
        // SPEC-0023 (Emenda v2): a leitura dos perfis que o cadastro de show (Events) declara no próprio contrato.
        services.AddScoped<IShowProfiles, ShowProfiles>();
        services.AddScoped<IPersonalDataSection, ProfilesPersonalDataSection>();

        services.AddModule<ProfilesDbContext>(
            configuration,
            typeof(ProfilesModule).Assembly,
            typeof(ProfilesErrors).Assembly,
            typeof(Profile).Assembly);
        services.ConfigureAllQuartzSchedulers(CleanupProfileRequestsJob.Schedule);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
