using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Diagnostics;
using Subsolo.Modules.Moderation.Domain.Reports;
using Subsolo.Modules.Moderation.Infrastructure.Reports;
using Subsolo.Modules.Moderation.Presentation;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Moderation.Infrastructure;

/// <summary>SPEC-0033 — registro do módulo Moderation: <c>services.AddModerationModule(configuration)</c> no host.</summary>
public static class ModerationModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.moderation.reports</c>, <c>subsolo.moderation.decisions</c>).</summary>
    public const string MeterName = ModerationTelemetry.MeterName;

    /// <summary>
    /// Chama <c>AddModule&lt;ModerationDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra o
    /// repositório, a unidade de trabalho, a trava por show, as leituras, a seção <c>reports</c> do export (<see cref="IPersonalDataSection"/>)
    /// e a política de rate limit <c>moderation-report</c> (10 denúncias por fã por dia).
    /// </summary>
    public static IServiceCollection AddModerationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IModerationUnitOfWork>(provider => provider.GetRequiredService<ModerationDbContext>());
        services.AddScoped<IShowLocks, ShowLocks>();
        services.AddScoped<IReportsReadModel, ReportsReadModel>();
        services.AddScoped<IPersonalDataSection, ModerationPersonalDataSection>();
        services.AddModerationRateLimit(configuration);

        // O assembly do domínio entra para a outbox achar o evento de domínio do ocultar.
        services.AddModule<ModerationDbContext>(
            configuration,
            typeof(ModerationModule).Assembly,
            typeof(ModerationErrors).Assembly,
            typeof(Report).Assembly);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
