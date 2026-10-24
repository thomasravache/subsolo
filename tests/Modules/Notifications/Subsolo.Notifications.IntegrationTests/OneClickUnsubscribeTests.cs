using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0031 H30-04 (Emenda 1, contrato v2; RFC 8058) — com o Mailpit real, os e-mails de aviso (resumo e lembrete) têm
/// os cabeçalhos <c>List-Unsubscribe: &lt;{App:PublicUrl}/api/v1/notifications/unsubscribe/one-click?token=…&gt;</c> e
/// <c>List-Unsubscribe-Post: List-Unsubscribe=One-Click</c>. Um <c>POST</c> nessa URL com o corpo
/// <c>application/x-www-form-urlencoded</c> <c>List-Unsubscribe=One-Click</c>, sem cookie nem <c>X-CSRF</c> (como o
/// provedor de e-mail faz), responde 204 e desliga só aquele tipo; o token adulterado → 400
/// <c>notifications.invalid_token</c>, sem mudar nada. O link da página <c>/avisos/descadastrar</c> continua no corpo.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed partial class OneClickUnsubscribeTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const string OneClickPath = "/api/v1/notifications/unsubscribe/one-click";

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-04")]
    [Trait("Kind", "Integration")]
    public async Task TheNoticeEmailsCarryTheOneClickHeadersAndThePostTurnsOffOnlyThatKind()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"fa.umclique-{Guid.NewGuid():N}@subsolo.test";
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, fanId) = await api.SignedInAsync(email, ct);
        using var _ = fan;
        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: false, dailyDigestEmail: true, reminderPush: false, reminderEmail: true),
            ct))
        {
            await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        await EnqueueAsync(api, fanId, new Notice("digest", $"h30-04-{Guid.NewGuid():N}", "Shows novos de quem você segue", "22/10 · Necrose · Porão, Curitiba", "https://localhost/"), ct);
        await EnqueueAsync(api, fanId, new Notice("reminder", $"h30-04-{Guid.NewGuid():N}", "Amanhã: Necrose no Porão", "Sábado, 21h", "https://localhost/curitiba/necrose"), ct);
        var messages = await mailpit.WaitForMessagesAsync(email, 2, ct);
        messages.Count.ShouldBe(2, "os dois avisos por e-mail não chegaram ao Mailpit");
        var digest = messages.Single(message => message.Subject.Contains("Shows novos", StringComparison.Ordinal));
        var reminder = messages.Single(message => message.Subject.Contains("Amanhã", StringComparison.Ordinal));

        var digestUrl = OneClickUrl(digest);
        OneClickUrl(reminder).ShouldNotBe(digestUrl, "o lembrete e o resumo têm o mesmo link de descadastro");
        foreach (var message in new[] { digest, reminder })
        {
            Header(message, "List-Unsubscribe-Post").ShouldBe("List-Unsubscribe=One-Click", $"cabeçalhos:\n{Headers(message)}");
            PageLink().IsMatch(message.Text).ShouldBeTrue($"o corpo perdeu o link de /avisos/descadastrar:\n{message.Text}");
        }

        var token = Uri.UnescapeDataString(new Uri(digestUrl).Query.TrimStart('?').Split('&')
            .Single(pair => pair.StartsWith("token=", StringComparison.Ordinal))["token=".Length..]);
        using var provider = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(NotificationsApiFactory.PublicUrl),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        using (var tampered = await PostOneClickAsync(provider, Tamper(token), ct))
        {
            await tampered.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "notifications.invalid_token", ct);
        }

        (await fan.PreferencesAsync(ct)).GetProperty("dailyDigestEmail").GetBoolean()
            .ShouldBeTrue("o token adulterado desligou o resumo diário");

        using (var unsubscribe = await PostOneClickAsync(provider, token, ct))
        {
            unsubscribe.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, NotificationsApiClient.Excerpt(await unsubscribe.Content.ReadAsStringAsync(ct)));
        }

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: false, reminderPush: false, reminderEmail: true, pushDevices: 0);
    }

    /// <summary>O <c>POST</c> do provedor de e-mail (RFC 8058): sem cookie, sem <c>X-CSRF</c>, corpo de formulário.</summary>
    private static async Task<HttpResponseMessage> PostOneClickAsync(HttpClient provider, string token, CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent([new KeyValuePair<string, string>("List-Unsubscribe", "One-Click")]);
        return await provider.PostAsync(new Uri($"{OneClickPath}?token={Uri.EscapeDataString(token)}", UriKind.Relative), body, ct);
    }

    private static async Task EnqueueAsync(NotificationsApiFactory api, Guid userId, Notice notice, CancellationToken ct)
    {
        var enqueued = await api.EnqueueAsync([userId], notice, ct);
        enqueued.IsSuccess.ShouldBeTrue(enqueued.IsFailure ? $"{enqueued.Error.Code} — {enqueued.Error.Description}" : null);
    }

    /// <summary>A URL do <c>List-Unsubscribe</c> (entre &lt; e &gt;), no endereço público e na rota de um clique.</summary>
    private static string OneClickUrl(MailpitMail message)
    {
        var value = Header(message, "List-Unsubscribe");
        var match = AngleUrl().Match(value);
        match.Success.ShouldBeTrue($"List-Unsubscribe sem URL entre < >: \"{value}\"");
        var url = match.Groups["url"].Value;
        url.ShouldStartWith($"{NotificationsApiFactory.PublicUrl}{OneClickPath}?token=", Case.Sensitive, url);
        return url;
    }

    /// <summary>O valor do cabeçalho <paramref name="name"/> da mensagem (linhas dobradas desfeitas).</summary>
    private static string Header(MailpitMail message, string name) =>
        Headers(message)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(name.Length + 1)..].Trim())
            .ShouldHaveSingleItem($"o e-mail não tem (exatamente um) cabeçalho {name}:\n{Headers(message)}");

    /// <summary>O bloco de cabeçalhos do código-fonte MIME, com as continuações (RFC 5322 §2.2.3) juntadas.</summary>
    private static string Headers(MailpitMail message)
    {
        var raw = message.Raw.Replace("\r\n", "\n", StringComparison.Ordinal);
        var end = raw.IndexOf("\n\n", StringComparison.Ordinal);
        var block = end < 0 ? raw : raw[..end];
        return Folding().Replace(block, " ");
    }

    /// <summary>Troca um caractere no meio da assinatura: o token deixa de bater com o HMAC.</summary>
    private static string Tamper(string token)
    {
        var index = token.Length - 10;
        return string.Concat(token.AsSpan(0, index), token[index] == 'A' ? "B" : "A", token.AsSpan(index + 1));
    }

    [GeneratedRegex(@"\n[ \t]+")]
    private static partial Regex Folding();

    [GeneratedRegex(@"<(?<url>[^>]+)>")]
    private static partial Regex AngleUrl();

    [GeneratedRegex(@"https://localhost/avisos/descadastrar\?token=[A-Za-z0-9._~%-]+")]
    private static partial Regex PageLink();
}
