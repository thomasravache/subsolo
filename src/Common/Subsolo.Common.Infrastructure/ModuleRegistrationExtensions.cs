using System.Reflection;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Infrastructure.Inbox;
using Subsolo.Common.Infrastructure.Messaging;
using Subsolo.Common.Infrastructure.Outbox;

namespace Subsolo.Common.Infrastructure;

/// <summary>SPEC-0007 — peças comuns do registro de um módulo, usadas pelo <c>Add&lt;M&gt;Module</c> de cada módulo.</summary>
public static class ModuleRegistrationExtensions
{
    /// <summary>Intervalo dos jobs <c>ProcessOutbox</c>/<c>ProcessInbox</c> de cada módulo.</summary>
    public static readonly TimeSpan ProcessingInterval = TimeSpan.FromSeconds(2);

    private static readonly Type[] HandlerContracts =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IDomainEventHandler<>),
        typeof(IIntegrationEventHandler<>),
    ];

    /// <summary>
    /// Registra o <typeparamref name="TDbContext"/> do módulo (PostgreSQL, connection string
    /// <see cref="CommonInfrastructureExtensions.ConnectionStringName"/>, histórico de migrações no schema do módulo,
    /// interceptor da outbox), varre <paramref name="assemblies"/> (Scrutor) atrás de handlers de comando/consulta,
    /// validadores, <c>IDomainEventHandler</c> e <c>IIntegrationEventHandler</c>, aplica os decorators de validação
    /// e log aos handlers deste módulo, inscreve os eventos de integração consumidos na inbox do módulo e agenda os
    /// jobs <c>ProcessOutbox</c>/<c>ProcessInbox</c> do módulo (a cada <see cref="ProcessingInterval"/>). Os
    /// endpoints são registrados à parte, com <c>AddEndpoints</c>.
    /// </summary>
    public static IServiceCollection AddModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
        where TDbContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Assembly[] moduleAssemblies = assemblies is { Length: > 0 } ? assemblies : [typeof(TDbContext).Assembly];
        var module = new ModuleDescriptor(typeof(TDbContext), moduleAssemblies);
        services.AddSingleton(module);

        services.AddDbContext<TDbContext>((provider, options) => options
            .UseNpgsql(CommonInfrastructureExtensions.ConnectionString(configuration))
            .AddInterceptors(provider.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        foreach (var descriptor in ModuleHandlers(moduleAssemblies))
        {
            services.Add(descriptor);
        }

        services.ConfigureAllQuartzSchedulers(quartz =>
        {
            Schedule<ProcessOutboxJob<TDbContext>>(quartz, $"{module.Name}.ProcessOutbox");
            Schedule<ProcessInboxJob<TDbContext>>(quartz, $"{module.Name}.ProcessInbox");
        });

        return services;
    }

    /// <summary>
    /// Handlers e validadores do módulo, já decorados. A varredura e a decoração acontecem numa coleção à parte,
    /// para que os decorators valham só para os handlers deste módulo (sem decorar de novo os de outros módulos).
    /// </summary>
    private static ServiceCollection ModuleHandlers(Assembly[] assemblies)
    {
        var handlers = new ServiceCollection();
        handlers.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableToAny(HandlerContracts), publicOnly: false)
            .As(type => type.GetInterfaces().Where(IsHandlerContract))
            .WithScopedLifetime());
        handlers.AddValidatorsFromAssemblies(assemblies, ServiceLifetime.Scoped, includeInternalTypes: true);

        // A ordem define o aninhamento: o log fica por fora da validação.
        handlers.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandHandler<>));
        handlers.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        handlers.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandHandler<>));
        handlers.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        handlers.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));
        return handlers;
    }

    private static bool IsHandlerContract(Type contract) =>
        contract.IsGenericType && HandlerContracts.Contains(contract.GetGenericTypeDefinition());

    private static void Schedule<TJob>(IQuartzBuilder quartz, string identity)
        where TJob : IJob =>
        quartz.ScheduleJob<TJob>(
            trigger => trigger
                .WithIdentity(identity)
                .StartNow()
                .WithSimpleSchedule(schedule => schedule.WithInterval(ProcessingInterval).RepeatForever()),
            job => job.WithIdentity(identity).DisallowConcurrentExecution());
}
