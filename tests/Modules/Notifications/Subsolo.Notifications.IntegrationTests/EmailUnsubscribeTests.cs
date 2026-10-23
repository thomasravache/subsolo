using System.Net;
using System.Text.RegularExpressions;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-05 (ADR-0014, ADR-0008) — com o Mailpit real: um fã com o resumo diário e o lembrete por e-mail ligados
/// recebe o aviso <c>digest</c> por e-mail (o e-mail vem do Users, por <c>IRecipientDirectory</c>), com o título, o corpo
/// e o link de descadastro <c>{App:PublicUrl}/avisos/descadastrar?token=…</c> nas partes texto e HTML;
/// <c>POST /api/v1/notifications/unsubscribe</c> anônimo com o token (e <c>X-CSRF</c>) responde 204 e desliga só aquele
/// tipo (o resumo diário; o lembrete por e-mail continua ligado); o token adulterado dá 400
/// <c>notifications.invalid_token</c> sem mudar nada.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed partial class EmailUnsubscribeTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0030:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task TheNoticeEmailHasTheUnsubscribeLinkThatTurnsOffOnlyThatKind()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"fa.resumo-{Guid.NewGuid():N}@subsolo.test";
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, fanId) = await api.SignedInAsync(email, ct);
        using var _ = fan;
        using var anonymous = api.CreateApiClient();
        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: false, dailyDigestEmail: true, reminderPush: true, reminderEmail: true),
            ct))
        {
            await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        var notice = new Notice(
            "digest", $"it05-{Guid.NewGuid():N}", "Seu resumo do dia no Subsolo", "3 shows novos de quem você segue", "https://localhost/seguindo");
        var enqueued = await api.EnqueueAsync([fanId], notice, ct);
        enqueued.IsSuccess.ShouldBeTrue(enqueued.IsFailure ? $"{enqueued.Error.Code} — {enqueued.Error.Description}" : null);

        var message = (await mailpit.WaitForMessagesAsync(email, 1, ct))
            .ShouldHaveSingleItem("o aviso por e-mail não chegou ao Mailpit (ou chegou mais de uma vez)");
        message.Text.ShouldContain(notice.Body, Case.Sensitive, message.Text);
        $"{message.Subject}\n{message.Text}".ShouldContain(notice.Title, Case.Sensitive, message.Subject);
        var link = UnsubscribeLink().Match(message.Text);
        link.Success.ShouldBeTrue($"o texto do e-mail não tem o link de descadastro:\n{message.Text}");
        WebUtility.HtmlDecode(message.Html).ShouldContain(link.Value, Case.Sensitive, "o HTML não tem o mesmo link de descadastro");
        var token = Uri.UnescapeDataString(link.Groups["token"].Value);

        using (var tampered = await anonymous.PostAsync(NotificationsRequests.Unsubscribe, new { token = Tamper(token) }, ct))
        {
            await tampered.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "notifications.invalid_token", ct);
        }

        (await fan.PreferencesAsync(ct)).GetProperty("dailyDigestEmail").GetBoolean()
            .ShouldBeTrue("o token adulterado desligou o resumo diário");

        using (var unsubscribe = await anonymous.PostAsync(NotificationsRequests.Unsubscribe, new { token }, ct))
        {
            unsubscribe.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, NotificationsApiClient.Excerpt(await unsubscribe.Content.ReadAsStringAsync(ct)));
        }

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: false, reminderPush: true, reminderEmail: true, pushDevices: 0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task AGarbageTokenIs400InvalidToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        using var anonymous = api.CreateApiClient();

        using var response = await anonymous.PostAsync(NotificationsRequests.Unsubscribe, new { token = "nao-e-um-token" }, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "notifications.invalid_token", ct);
    }

    /// <summary>
    /// Troca um caractere no meio da assinatura (o último pode só carregar bits de preenchimento do base64url): o token
    /// deixa de bater com o HMAC.
    /// </summary>
    private static string Tamper(string token)
    {
        var index = token.Length - 10;
        return string.Concat(token.AsSpan(0, index), token[index] == 'A' ? "B" : "A", token.AsSpan(index + 1));
    }

    [GeneratedRegex(@"https://localhost/avisos/descadastrar\?token=(?<token>[A-Za-z0-9._~%-]+)")]
    private static partial Regex UnsubscribeLink();
}
