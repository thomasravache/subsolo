using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Diagnostics;
using Subsolo.Modules.Notifications.Application.Features.SendEmail;
using Subsolo.Modules.Notifications.Infrastructure.Email;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Infrastructure;

/// <summary>SPEC-0018 — registro do módulo Notifications: <c>services.AddNotificationsModule(configuration)</c> no host.</summary>
public static class NotificationsModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.notifications.emails</c>).</summary>
    public const string MeterName = NotificationsTelemetry.MeterName;

    /// <summary>
    /// Registra <c>Notifications:Smtp</c> (validada na inicialização: TLS obrigatório em Production), o canal de
    /// e-mail por SMTP e <see cref="INotificationsApi"/>. Ainda sem tabelas nem endpoints.
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>());

        services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
        services.AddScoped<INotificationsApi, NotificationsApi>();
        return services;
    }
}
