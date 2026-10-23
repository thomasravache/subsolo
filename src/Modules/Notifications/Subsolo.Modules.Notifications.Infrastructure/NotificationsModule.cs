using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Notifications.Application;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Diagnostics;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;
using Subsolo.Modules.Notifications.Application.Features.SendEmail;
using Subsolo.Modules.Notifications.Application.Features.Unsubscribe;
using Subsolo.Modules.Notifications.Domain.Preferences;
using Subsolo.Modules.Notifications.Infrastructure.Deliveries;
using Subsolo.Modules.Notifications.Infrastructure.Email;
using Subsolo.Modules.Notifications.Infrastructure.Persistence;
using Subsolo.Modules.Notifications.Infrastructure.Unsubscribe;
using Subsolo.Modules.Notifications.Infrastructure.WebPush;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Notifications.Presentation;

namespace Subsolo.Modules.Notifications.Infrastructure;

/// <summary>SPEC-0018 — registro do módulo Notifications: <c>services.AddNotificationsModule(configuration)</c> no host.</summary>
public static class NotificationsModule
{
    /// <summary>
    /// Nome do <c>Meter</c> do módulo (<c>subsolo.notifications.emails</c>; SPEC-0030:
    /// <c>subsolo.notifications.deliveries</c> e <c>subsolo.notifications.subscriptions_removed</c>).
    /// </summary>
    public const string MeterName = NotificationsTelemetry.MeterName;

    /// <summary>
    /// Registra <c>Notifications:Smtp</c> (validada na inicialização: TLS obrigatório em Production), o canal de
    /// e-mail por SMTP e <see cref="INotificationsApi"/>.
    /// SPEC-0030 — o schema <c>notifications</c> (<c>AddModule&lt;NotificationsDbContext&gt;</c>, migrações), as
    /// inscrições, preferências e o descadastro (endpoints do Presentation), o canal Web Push
    /// (<c>Notifications:WebPush</c>, validada na inicialização quando configurada; o <c>HttpClient</c>
    /// <see cref="WebPushOptions.HttpClientName"/> sem o handler de resiliência padrão: as novas tentativas são das
    /// entregas), o token de descadastro (<c>Notifications:Unsubscribe</c>) e o job de entregas
    /// (<see cref="ProcessDeliveriesJob"/>).
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>());
        services.AddOptions<WebPushOptions>()
            .Bind(configuration.GetSection(WebPushOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WebPushOptions>, WebPushOptionsValidator>());
        services.AddOptions<UnsubscribeOptions>().Bind(configuration.GetSection(UnsubscribeOptions.SectionName));
        services.AddOptions<NoticeLinksOptions>().Bind(configuration.GetSection(NoticeLinksOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
        services.AddSingleton<WebPushKeys>();
        services.AddSingleton<IWebPushKeys>(provider => provider.GetRequiredService<WebPushKeys>());
        services.AddSingleton<VapidTokenCache>();
        services.AddSingleton<INotificationChannel, WebPushChannel>();
        // O handler de resiliência do ServiceDefaults repetiria um push já aceito depois de um tempo esgotado; as novas
        // tentativas ficam com as entregas, que nunca reenviam a um aparelho que aceitou (ADR-0014).
#pragma warning disable EXTEXP0001 // API experimental do Microsoft.Extensions.Http.Resilience, versão fixada no Directory.Packages.props
        services.AddHttpClient(WebPushOptions.HttpClientName).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddSingleton<IUnsubscribeTokens, UnsubscribeTokens>();

        services.AddScoped<INotificationsApi, NotificationsApi>();
        services.AddScoped<INotificationsStore, NotificationsStore>();
        services.AddScoped<NoticeQueue>();
        services.AddScoped<DeliverySender>();

        services.AddModule<NotificationsDbContext>(
            configuration,
            typeof(NotificationsModule).Assembly,
            typeof(NotificationsApi).Assembly,
            typeof(NotificationPreferences).Assembly);
        services.ConfigureAllQuartzSchedulers(ProcessDeliveriesJob.Schedule);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
