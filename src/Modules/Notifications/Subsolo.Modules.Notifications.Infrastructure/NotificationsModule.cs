using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Features.SendEmail;
using Subsolo.Modules.Notifications.Infrastructure.Email;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Infrastructure;

/// <summary>SPEC-0018 — registro do módulo Notifications: <c>services.AddNotificationsModule(configuration)</c> no host.</summary>
public static class NotificationsModule
{
    /// <summary>
    /// Scaffold do contrato: <see cref="INotificationsApi"/> e o canal de e-mail existem no contêiner, mas ainda não
    /// estão implementados (a implementação também registra <c>Notifications:Smtp</c> com validação na inicialização).
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
        services.AddScoped<INotificationsApi, NotificationsApi>();
        return services;
    }
}
