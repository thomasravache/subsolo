using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Infrastructure;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.UnitTests.Infrastructure;

/// <summary>
/// O módulo Notifications registrado como no host (<c>AddNotificationsModule</c>), no ambiente
/// <paramref name="environment"/>, com o canal de e-mail trocado por <see cref="CapturingEmailChannel"/>: nada sai
/// por SMTP, e o teste lê o e-mail renderizado que chegaria ao canal (ou confirma que o canal não foi chamado).
/// </summary>
internal sealed class NotificationsHarness : IAsyncDisposable
{
    public const string Recipient = "fa@subsolo.test";

    public const string Link = "https://subsolo.test/entrar/confirmar?token=abc";

    private readonly ServiceProvider _provider;

    public NotificationsHarness(string environment = "Development")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(SmtpSettings("StartTls"))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment));
        services.AddSingleton(TimeProvider.System);
        services.AddNotificationsModule(configuration);
        services.RemoveAll<INotificationChannel>();
        services.AddSingleton<INotificationChannel>(Channel);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public CapturingEmailChannel Channel { get; } = new();

    /// <summary>Configuração <c>Notifications:Smtp</c> válida, com a segurança <paramref name="security"/>.</summary>
    public static Dictionary<string, string?> SmtpSettings(string security) => new(StringComparer.Ordinal)
    {
        ["Notifications:Smtp:Host"] = "smtp.subsolo.test",
        ["Notifications:Smtp:Port"] = "587",
        ["Notifications:Smtp:Security"] = security,
        ["Notifications:Smtp:FromAddress"] = "nao-responda@subsolo.test",
    };

    /// <summary>Pedido do modelo <see cref="EmailTemplate.MagicLink"/> com <c>link</c> e <c>minutes</c>.</summary>
    public static EmailRequest MagicLink(string link = Link, string minutes = "15", string to = Recipient) =>
        new(to, EmailTemplate.MagicLink, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["link"] = link,
            ["minutes"] = minutes,
        });

    /// <summary><see cref="INotificationsApi.SendEmailAsync"/> num escopo novo, como uma requisição.</summary>
    public async Task<Result> SendAsync(EmailRequest request)
    {
        await using var scope = _provider.CreateAsyncScope();
        var api = scope.ServiceProvider.GetRequiredService<INotificationsApi>();
        return await api.SendEmailAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Envia <paramref name="request"/>, exige sucesso e devolve o único e-mail que chegou ao canal.</summary>
    public async Task<EmailNotification> RenderAsync(EmailRequest request)
    {
        var result = await SendAsync(request);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"{result.Error.Code}: {result.Error.Description}" : null);
        return Channel.Sent.ShouldHaveSingleItem("o e-mail não chegou ao canal (ou chegou mais de uma vez)");
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Subsolo.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>Dublê do canal de e-mail: guarda cada mensagem recebida e responde sucesso.</summary>
internal sealed class CapturingEmailChannel : INotificationChannel
{
    private readonly List<Notification> _received = [];

    public NotificationChannel Channel => NotificationChannel.Email;

    /// <summary>Quantas vezes o canal foi chamado (com qualquer mensagem).</summary>
    public int Calls
    {
        get
        {
            lock (_received)
            {
                return _received.Count;
            }
        }
    }

    public IReadOnlyList<EmailNotification> Sent
    {
        get
        {
            lock (_received)
            {
                return [.. _received.OfType<EmailNotification>()];
            }
        }
    }

    public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        lock (_received)
        {
            _received.Add(notification);
        }

        return Task.FromResult(Result.Success());
    }
}
