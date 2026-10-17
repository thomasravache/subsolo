using System.Diagnostics;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0018 IT-03 — com um servidor TCP que aceita a conexão e não responde, e depois com uma porta fechada,
/// <c>SendEmailAsync</c> devolve <c>notifications.email_unavailable</c> sem lançar exceção, em até 10 s (o timeout por
/// envio; mais 1 s de folga para o agendamento da máquina de CI).
/// </summary>
public sealed class SmtpUnavailableTests
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task SilentServerReturnsEmailUnavailableWithinTheTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var silent = SilentSmtpServer.Start();
        await using var host = await NotificationsHost.StartAsync(SilentSmtpServer.Host, silent.Port, ct);

        var (result, elapsed) = await SendAsync(host, ct);

        silent.Accepted.ShouldBeGreaterThan(0, "o envio nem chegou a conectar no servidor mudo");
        ShouldBeUnavailable(result, elapsed);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task ClosedPortReturnsEmailUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await NotificationsHost.StartAsync(SilentSmtpServer.Host, SilentSmtpServer.ClosedPort(), ct);

        var (result, elapsed) = await SendAsync(host, ct);

        ShouldBeUnavailable(result, elapsed);
    }

    private static async Task<(Result Result, TimeSpan Elapsed)> SendAsync(NotificationsHost host, CancellationToken ct)
    {
        var request = NotificationsHost.MagicLink(
            $"fa-{Guid.NewGuid():N}@subsolo.test", $"https://subsolo.test/entrar/confirmar?token=it03-{Guid.NewGuid():N}");
        var watch = Stopwatch.StartNew();
        Result? result = null;
        await Should.NotThrowAsync(async () => result = await host.SendAsync(request, ct), "SMTP fora do ar derrubou o chamador");
        watch.Stop();
        return (result.ShouldNotBeNull(), watch.Elapsed);
    }

    private static void ShouldBeUnavailable(Result result, TimeSpan elapsed)
    {
        result.IsFailure.ShouldBeTrue("envio com SMTP fora do ar deu sucesso");
        result.Error.Code.ShouldBe(NotificationsErrors.EmailUnavailable);
        elapsed.ShouldBeLessThanOrEqualTo(SendTimeout + Slack, $"o envio levou {elapsed.TotalSeconds:F1} s");
    }
}
