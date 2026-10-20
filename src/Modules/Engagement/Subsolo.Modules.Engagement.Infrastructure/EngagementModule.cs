using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Application.Features.GetFollowers;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.Infrastructure.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Engagement.Presentation;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>SPEC-0027 — registro do módulo Engagement: <c>services.AddEngagementModule(configuration)</c> no host.</summary>
public static class EngagementModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.engagement.follows</c>, <c>subsolo.engagement.unfollows</c>).</summary>
    public const string MeterName = EngagementTelemetry.MeterName;

    /// <summary>
    /// Chama <c>AddModule&lt;EngagementDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra o
    /// repositório, a unidade de trabalho, as leituras e remoções, a interface pública <see cref="IEngagementApi"/> e a
    /// seção <c>follows</c> do export (<see cref="IPersonalDataSection"/>).
    /// </summary>
    public static IServiceCollection AddEngagementModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddScoped<IEngagementUnitOfWork>(provider => provider.GetRequiredService<EngagementDbContext>());
        services.AddScoped<IFollowsReadModel, FollowsReadModel>();
        services.AddScoped<IFollowsMaintenance, FollowsMaintenance>();
        services.AddScoped<IEngagementApi, EngagementApi>();
        services.AddScoped<IPersonalDataSection, EngagementPersonalDataSection>();

        services.AddModule<EngagementDbContext>(
            configuration,
            typeof(EngagementModule).Assembly,
            typeof(EngagementErrors).Assembly,
            typeof(Follow).Assembly);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
