using System.Security.Cryptography;
using Subsolo.Modules.Notifications.Application.Features.Subscriptions;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0030 UT-01 (§5, contra SSRF) — o validador de inscrição aceita só endpoints <c>https</c> dos serviços de push
/// conhecidos (FCM <c>fcm.googleapis.com</c>, Mozilla <c>updates.push.services.mozilla.com</c>, Apple
/// <c>web.push.apple.com</c>) com as chaves <c>p256dh</c> e <c>auth</c>; <c>http</c>, domínio desconhecido (inclusive
/// parecido: sufixo, usuário na URL, caminho), URL relativa e chaves ausentes ou vazias dão
/// <c>notifications.invalid_subscription</c>.
/// </summary>
public sealed class PushSubscriptionValidatorTests
{
    private static readonly string P256dh = NewP256dh();

    private static readonly string Auth = Base64Url(RandomNumberGenerator.GetBytes(16));

    public static TheoryData<string> KnownServices => new()
    {
        "https://fcm.googleapis.com/fcm/send/dGVzdGU6QVBBOTFiSA",
        "https://updates.push.services.mozilla.com/wpush/v2/gAAAAABk",
        "https://web.push.apple.com/QGuQyavXutnMH7xBkr3Lx0fX",
    };

    public static TheoryData<string> UnknownEndpoints => new()
    {
        "http://fcm.googleapis.com/fcm/send/abc",
        "http://updates.push.services.mozilla.com/wpush/v2/abc",
        "https://evil.com/fcm/send/abc",
        "https://fcm.googleapis.com.evil.com/fcm/send/abc",
        "https://evil.com/fcm.googleapis.com/abc",
        "https://fcm.googleapis.com@evil.com/fcm/send/abc",
        "https://127.0.0.1/fcm/send/abc",
        "https://localhost/push",
        "ftp://fcm.googleapis.com/fcm/send/abc",
        "/fcm/send/abc",
        "",
    };

    [Theory]
    [Trait("Spec", "SPEC-0030:UT-01")]
    [MemberData(nameof(KnownServices))]
    public void AnHttpsEndpointOfAKnownServiceWithKeysIsAccepted(string endpoint)
    {
        var result = PushSubscriptionValidator.Validate(endpoint, P256dh, Auth);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"{endpoint}: {result.Error.Code}" : null);
    }

    [Theory]
    [Trait("Spec", "SPEC-0030:UT-01")]
    [MemberData(nameof(UnknownEndpoints))]
    public void AnEndpointOutsideTheKnownServicesIsRejected(string endpoint)
    {
        var result = PushSubscriptionValidator.Validate(endpoint, P256dh, Auth);

        result.IsFailure.ShouldBeTrue($"endpoint aceito: \"{endpoint}\"");
        result.Error.Code.ShouldBe("notifications.invalid_subscription");
    }

    [Theory]
    [Trait("Spec", "SPEC-0030:UT-01")]
    [InlineData(null, "auth")]
    [InlineData("", "auth")]
    [InlineData("p256dh", null)]
    [InlineData("p256dh", "")]
    [InlineData(null, null)]
    public void MissingKeysAreRejectedEvenForAKnownService(string? p256dh, string? auth)
    {
        var result = PushSubscriptionValidator.Validate(
            "https://fcm.googleapis.com/fcm/send/dGVzdGU6QVBBOTFiSA",
            p256dh == "p256dh" ? P256dh : p256dh,
            auth == "auth" ? Auth : auth);

        result.IsFailure.ShouldBeTrue($"chaves ausentes aceitas: p256dh={p256dh ?? "null"}, auth={auth ?? "null"}");
        result.Error.Code.ShouldBe("notifications.invalid_subscription");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-01")]
    public void AMissingEndpointIsRejected()
    {
        var result = PushSubscriptionValidator.Validate(null, P256dh, Auth);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("notifications.invalid_subscription");
    }

    private static string NewP256dh()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        return Base64Url([0x04, .. point.X!, .. point.Y!]);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
