using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;
using Subsolo.Modules.Engagement.Application.Features.GetAttendees;
using Subsolo.Modules.Engagement.Application.Features.GetFollowers;
using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.Infrastructure.Attendances;
using Subsolo.Modules.Engagement.Infrastructure.Follows;
using Subsolo.Modules.Engagement.Infrastructure.Notices;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Engagement.Presentation;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>SPEC-0027 — registro do módulo Engagement: <c>services.AddEngagementModule(configuration)</c> no host.</summary>
public static class EngagementModule
{
    /// <summary>
    /// Nome do <c>Meter</c> do módulo (<c>subsolo.engagement.follows</c>, <c>subsolo.engagement.unfollows</c>,
    /// <c>subsolo.engagement.attendances</c>; SPEC-0031: <c>subsolo.engagement.new_show_notices</c> e
    /// <c>subsolo.engagement.digest_recipients</c>).
    /// </summary>
    public const string MeterName = EngagementTelemetry.MeterName;

    /// <summary>
    /// Chama <c>AddModule&lt;EngagementDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra o
    /// repositório, a unidade de trabalho, as leituras e remoções, a interface pública <see cref="IEngagementApi"/> e a
    /// seção <c>follows</c> do export (<see cref="IPersonalDataSection"/>). SPEC-0029: o mesmo para os "vou" (a interface
    /// pública <see cref="IAttendanceApi"/> e a seção <c>attendances</c>). SPEC-0031: o registro dos shows publicados
    /// (<see cref="IPublishedShows"/>), o montador do resumo e o job <see cref="DailyDigestJob"/>
    /// (<c>Engagement:DigestCron</c>); o consumidor do <c>ShowPublished</c> entra pela varredura do <c>AddModule</c>.
    /// SPEC-0032: os dias com resumo concluído (<see cref="IDigestRuns"/>, base da recuperação
    /// <c>Engagement:DigestCatchUpCron</c>).
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
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAttendancesReadModel, AttendancesReadModel>();
        services.AddScoped<IAttendancesMaintenance, AttendancesMaintenance>();
        services.AddScoped<IAttendanceApi, AttendanceApi>();
        services.AddScoped<IPersonalDataSection, AttendancesPersonalDataSection>();
        services.AddScoped<IPublishedShows, PublishedShows>();
        services.AddScoped<DigestBuilder>();
        services.AddScoped<IDigestRuns, DigestRuns>();

        services.AddModule<EngagementDbContext>(
            configuration,
            typeof(EngagementModule).Assembly,
            typeof(EngagementErrors).Assembly,
            typeof(Follow).Assembly);
        services.ConfigureAllQuartzSchedulers(quartz => DailyDigestJob.Schedule(quartz, configuration));
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
