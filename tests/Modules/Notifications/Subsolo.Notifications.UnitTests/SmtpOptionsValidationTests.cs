using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Subsolo.Modules.Notifications.Infrastructure;
using Subsolo.Notifications.UnitTests.Infrastructure;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0018 UT-03 — a configuração <c>Notifications:Smtp</c> é validada na inicialização do host: fora de
/// Development (Production, Staging — Emenda 2), <c>Security=None</c> impede o start com uma mensagem que nomeia
/// <c>Notifications:Smtp:Security</c>; em Production com
/// TLS (<c>StartTls</c> ou <c>SslOnConnect</c>) e em Development com <c>None</c> (Mailpit), o host sobe.
/// </summary>
public sealed class SmtpOptionsValidationTests
{
    [Theory]
    [Trait("Spec", "SPEC-0018:UT-03")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task OutsideDevelopmentWithoutTlsDoesNotStart(string environment)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = BuildHost(environment, "None");

        var failure = await Should.ThrowAsync<Exception>(() => host.StartAsync(ct), $"a API subiu em {environment} sem TLS no SMTP");

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.ToString().ShouldContain("Notifications:Smtp:Security", Case.Sensitive, failure.ToString());
    }

    [Theory]
    [Trait("Spec", "SPEC-0018:UT-03")]
    [InlineData("Production", "StartTls")]
    [InlineData("Production", "SslOnConnect")]
    [InlineData("Staging", "StartTls")]
    [InlineData("Development", "None")]
    public async Task AcceptedConfigurationStarts(string environment, string security)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = BuildHost(environment, security);

        await Should.NotThrowAsync(() => host.StartAsync(ct));
        await host.StopAsync(ct);
    }

    private static IHost BuildHost(string environment, string security)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ApplicationName = "Subsolo.Api",
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection(NotificationsHarness.SmtpSettings(security));
        builder.Services.AddNotificationsModule(builder.Configuration);
        return builder.Build();
    }
}
