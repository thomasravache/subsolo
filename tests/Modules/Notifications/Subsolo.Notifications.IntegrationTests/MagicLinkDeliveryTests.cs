using System.Diagnostics;
using System.Net;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0018 IT-01 — com o Mailpit real em container, 20 envios do <c>MagicLink</c> por <c>SendEmailAsync</c> chegam
/// todos: remetente "Subsolo" com o endereço configurado, assunto do §6, mensagem MIME com as partes HTML e texto
/// (<c>multipart/alternative</c>), e o link exato de cada envio nas duas partes; o p95 do envio fica ≤ 2 s.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class MagicLinkDeliveryTests(MailpitServer mailpit)
{
    private const int Sends = 20;
    private const string Subject = "Seu link para entrar no Subsolo";

    private static readonly TimeSpan P95Budget = TimeSpan.FromSeconds(2);

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwentyMagicLinksReachMailpitWithBothPartsAndTheExactLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var recipient = $"fa-{Guid.NewGuid():N}@subsolo.test";
        await using var host = await NotificationsHost.StartAsync(mailpit.SmtpHost, mailpit.SmtpPort, ct);

        var links = new List<string>();
        var durations = new List<TimeSpan>();
        for (var i = 0; i < Sends; i++)
        {
            var link = $"https://subsolo.test/entrar/confirmar?token=it01-{i:D2}-{Guid.NewGuid():N}";
            links.Add(link);
            var watch = Stopwatch.StartNew();
            var result = await host.SendAsync(NotificationsHost.MagicLink(recipient, link), ct);
            watch.Stop();
            result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"envio {i}: {result.Error.Code} — {result.Error.Description}" : null);
            durations.Add(watch.Elapsed);
        }

        var messages = await mailpit.WaitForMessagesAsync(recipient, Sends, ct);
        messages.Count.ShouldBe(Sends, "nem todas as mensagens chegaram ao Mailpit");

        var delivered = new List<string>();
        foreach (var message in messages)
        {
            message.FromName.ShouldBe("Subsolo", $"remetente de {message.Id}");
            message.FromAddress.ShouldBe(NotificationsHost.FromAddress, $"remetente de {message.Id}");
            message.To.ShouldBe([recipient]);
            message.Subject.ShouldBe(Subject);
            message.Raw.ShouldContain("multipart/alternative", Case.Insensitive, $"{message.Id} não tem HTML e texto alternativos");
            message.Raw.ShouldContain("text/plain", Case.Insensitive, $"{message.Id} sem parte em texto puro");
            message.Raw.ShouldContain("text/html", Case.Insensitive, $"{message.Id} sem parte HTML");

            var link = links.SingleOrDefault(candidate => message.Text.Contains(candidate, StringComparison.Ordinal))
                .ShouldNotBeNull($"o texto de {message.Id} não tem o link exato de nenhum envio:\n{message.Text}");
            WebUtility.HtmlDecode(message.Html).ShouldContain(link, Case.Sensitive, $"o HTML de {message.Id} não tem o link exato");
            delivered.Add(link);
        }

        delivered.ShouldBe(links, ignoreOrder: true, "cada envio chega uma vez, com o próprio link");

        var p95 = durations.Order().ElementAt((int)Math.Ceiling(0.95 * Sends) - 1);
        p95.ShouldBeLessThanOrEqualTo(P95Budget, $"p95 do envio = {p95.TotalMilliseconds:F0} ms");
    }
}
