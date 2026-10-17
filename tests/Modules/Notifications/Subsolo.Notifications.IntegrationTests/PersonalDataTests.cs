using System.Diagnostics;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0018 IT-04 (ADR-0008) — com os logs e spans exportados capturados em memória (OpenTelemetry, com o
/// mascaramento dos ServiceDefaults), um envio do <c>MagicLink</c> exporta o span <c>notifications.email.send</c>, e
/// nenhum log ou span traz o destinatário em claro, o link (uma credencial), o token, o assunto ou o corpo; quando o
/// envio falha, o log da falha traz o destinatário só mascarado.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class PersonalDataTests(MailpitServer mailpit)
{
    private const string SpanName = "notifications.email.send";

    /// <summary>Trechos do assunto e do corpo do MagicLink (§6) que nunca vão para logs e spans.</summary>
    private static readonly string[] Content =
    [
        "Seu link para entrar no Subsolo",
        "Entrar no Subsolo",
        "O link vale por",
        "Se não foi você",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task SendExportsTheSpanWithoutRecipientLinkOrContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (recipient, token, link) = NewMagicLink();
        await using var host = await NotificationsHost.StartAsync(mailpit.SmtpHost, mailpit.SmtpPort, ct);

        var result = await host.SendAsync(NotificationsHost.MagicLink(recipient, link), ct);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"{result.Error.Code} — {result.Error.Description}" : null);

        var spans = host.Spans;
        spans.ShouldContain(
            span => span.OperationName == SpanName || span.DisplayName == SpanName,
            $"span {SpanName} não exportado; spans: {string.Join(", ", spans.Select(s => s.DisplayName))}");
        Leaks(host, recipient, token, link).ShouldBeEmpty("dado pessoal ou conteúdo nos sinais exportados");
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task FailedSendLogsTheRecipientOnlyMasked()
    {
        var ct = TestContext.Current.CancellationToken;
        var (recipient, token, link) = NewMagicLink();
        await using var host = await NotificationsHost.StartAsync(SilentSmtpServer.Host, SilentSmtpServer.ClosedPort(), ct);

        var result = await host.SendAsync(NotificationsHost.MagicLink(recipient, link), ct);
        result.IsFailure.ShouldBeTrue("envio para uma porta fechada deu sucesso");
        result.Error.Code.ShouldBe(NotificationsErrors.EmailUnavailable);

        var texts = Texts(host).ToList();
        texts.ShouldContain(
            item => item.Text.Contains("***@", StringComparison.Ordinal),
            "a falha não foi logada com o destinatário mascarado");
        Leaks(host, recipient, token, link).ShouldBeEmpty("dado pessoal ou conteúdo nos sinais exportados");
    }

    private static (string Recipient, string Token, string Link) NewMagicLink()
    {
        var recipient = $"fa.sigiloso-{Guid.NewGuid():N}@subsolo.test";
        var token = $"tok{Guid.NewGuid():N}";
        return (recipient, token, $"https://subsolo.test/entrar/confirmar?token={token}");
    }

    private static List<string> Leaks(NotificationsHost host, string recipient, string token, string link)
    {
        string[] secrets = [recipient, token, link, .. Content];
        return [.. Texts(host)
            .SelectMany(item => secrets
                .Where(secret => item.Text.Contains(secret, StringComparison.OrdinalIgnoreCase))
                .Select(secret => $"{item.Source} contém \"{secret}\": {item.Text}"))
            .Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<(string Source, string Text)> Texts(NotificationsHost host)
    {
        foreach (var log in host.Logs)
        {
            var source = $"log {log.CategoryName}";
            foreach (var text in new[] { log.FormattedMessage, log.Body, log.Exception?.ToString() }
                .Concat((log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}")))
            {
                if (!string.IsNullOrEmpty(text))
                {
                    yield return (source, text);
                }
            }
        }

        foreach (var span in host.Spans)
        {
            var source = $"span {span.DisplayName}";
            foreach (var text in new[] { span.DisplayName, span.StatusDescription }
                .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}"))))
            {
                if (!string.IsNullOrEmpty(text))
                {
                    yield return (source, text);
                }
            }
        }
    }
}
