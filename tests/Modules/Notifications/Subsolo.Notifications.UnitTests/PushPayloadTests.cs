using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0030 UT-04 (Emenda da revisão G4, 2026-10-23; §6.1 "Payload do push") — o montador do conteúdo do push
/// (<see cref="PushPayload.Serialize"/>), com título de 120 e corpo de 1.000 caracteres (os máximos que o
/// <c>EnqueueAsync</c> aceita, <see cref="NoticeQueue.MaxTitleLength"/>/<see cref="NoticeQueue.MaxBodyLength"/>) feitos
/// só de letras acentuadas e uma URL de 200 caracteres: o JSON <c>{title, body, url}</c> traz as letras como são (sem
/// <c>á</c> etc.) e, cifrado em <c>aes128gcm</c> pela mesma biblioteca do canal (Lib.Net.Http.WebPush, para uma
/// inscrição gerada no teste e um servidor de push falso em memória), o corpo cabe nos 4.096 bytes do Web Push.
/// </summary>
public sealed partial class PushPayloadTests
{
    /// <summary>Limite do corpo de uma mensagem de Web Push aceito pelos serviços de push (FCM, Mozilla, Apple).</summary>
    private const int WebPushLimit = 4096;

    private const int UrlLength = 200;

    /// <summary>Letras acentuadas do português (2 bytes cada em UTF-8; 6 bytes cada como <c>\uXXXX</c>).</summary>
    private const string Accented = "áéíóúâêôãõçàÁÉÍÓÚÂÊÔÃÕÇÀ";

    private static readonly string Title = Letters(NoticeQueue.MaxTitleLength);

    private static readonly string Body = Letters(NoticeQueue.MaxBodyLength);

    private static readonly string Url = ("https://subsolo.app/cidade-exemplo/shows/" + new string('a', UrlLength))[..UrlLength];

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-04")]
    public void ThePayloadKeepsAccentedLettersUnescaped()
    {
        var json = PushPayload.Serialize(Title, Body, Url);

        var escapes = UnicodeEscape().Matches(json).Select(match => match.Value).Distinct().Take(5).ToList();
        escapes.ShouldBeEmpty($"o payload escapa letras acentuadas ({string.Join(", ", escapes)}…)");
        json.ShouldContain(Title);
        json.ShouldContain(Body);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).ShouldBe(["title", "body", "url"], ignoreOrder: true);
        root.GetProperty("title").GetString().ShouldBe(Title);
        root.GetProperty("body").GetString().ShouldBe(Body);
        root.GetProperty("url").GetString().ShouldBe(Url);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-04")]
    public async Task TheEncryptedPayloadFitsTheWebPushLimit()
    {
        var json = PushPayload.Serialize(Title, Body, Url);
        var capture = new CapturingPushService();

        await EncryptAndSendAsync(json, capture, TestContext.Current.CancellationToken);

        var body = capture.Body.ShouldNotBeNull("a biblioteca não chamou o serviço de push");
        body.Length.ShouldBeLessThanOrEqualTo(
            WebPushLimit,
            $"o push cifrado (aes128gcm) tem {body.Length} bytes; o payload tem {Encoding.UTF8.GetByteCount(json)} bytes em UTF-8");
    }

    /// <summary>Cifra e "envia" <paramref name="payload"/> como o canal faz, para uma inscrição P-256 gerada aqui.</summary>
    private static async Task EncryptAndSendAsync(string payload, CapturingPushService capture, CancellationToken ct)
    {
        using var vapidKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var vapid = vapidKey.ExportParameters(includePrivateParameters: true);
        using var browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var browser = browserKey.ExportParameters(includePrivateParameters: false);

        using var http = new HttpClient(capture);
        using var authentication = new VapidAuthentication(
            Base64Url([0x04, .. vapid.Q.X!, .. vapid.Q.Y!]), Base64Url(vapid.D!))
        {
            Subject = "mailto:avisos@subsolo.test",
        };
        var client = new PushServiceClient(http)
        {
            DefaultAuthentication = authentication,
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
            AutoRetryAfter = false,
        };
        var subscription = new PushSubscription { Endpoint = "https://fcm.googleapis.com/fcm/send/ut04" };
        subscription.SetKey(PushEncryptionKeyName.P256DH, Base64Url([0x04, .. browser.Q.X!, .. browser.Q.Y!]));
        subscription.SetKey(PushEncryptionKeyName.Auth, Base64Url(RandomNumberGenerator.GetBytes(16)));

        await client.RequestPushMessageDeliveryAsync(subscription, new PushMessage(payload), ct);
    }

    private static string Letters(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(index => Accented[index % Accented.Length]));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex(@"\\u[0-9A-Fa-f]{4}")]
    private static partial Regex UnicodeEscape();

    /// <summary>Servidor de push falso em memória: guarda o corpo cifrado e responde 201.</summary>
    private sealed class CapturingPushService : HttpMessageHandler
    {
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created) { RequestMessage = request };
        }
    }
}
