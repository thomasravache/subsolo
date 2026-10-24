using System.Security.Cryptography;
using Subsolo.Modules.Notifications.Application.Features.Subscriptions;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0031 H30-01 (item herdado da revisão da SPEC-0030) — o validador de inscrição aceita o WNS do Edge no Windows: um
/// endpoint <c>https</c> cujo host termina exatamente em <c>.notify.windows.com</c> (ex.:
/// <c>wns2-bl2p.notify.windows.com</c>), com as chaves; o host parecido (<c>notify.windows.com.evil.example</c>, sufixo
/// sem o ponto), o <c>http</c>, a porta própria e o usuário na URL continuam recusados com
/// <c>notifications.invalid_subscription</c>.
/// </summary>
public sealed class WnsSubscriptionTests
{
    private static readonly string P256dh = NewP256dh();

    private static readonly string Auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static TheoryData<string> WnsEndpoints => new()
    {
        "https://wns2-bl2p.notify.windows.com/w/?token=aparelho-de-teste",
        "https://wns2-par02p.notify.windows.com/w/?token=outro-aparelho",
    };

    public static TheoryData<string> LookAlikes => new()
    {
        "https://notify.windows.com.evil.example/w/?token=abc",
        "https://evilnotify.windows.com/w/?token=abc",
        "http://wns2-bl2p.notify.windows.com/w/?token=abc",
        "https://wns2-bl2p.notify.windows.com:8443/w/?token=abc",
        "https://user@wns2-bl2p.notify.windows.com/w/?token=abc",
        "https://evil.example/wns2-bl2p.notify.windows.com/w/?token=abc",
    };

    [Theory]
    [Trait("Spec", "SPEC-0031:H30-01")]
    [MemberData(nameof(WnsEndpoints))]
    public void AnHttpsWnsEndpointWithKeysIsAccepted(string endpoint)
    {
        var result = PushSubscriptionValidator.Validate(endpoint, P256dh, Auth);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"{endpoint}: {result.Error.Code}" : null);
    }

    [Theory]
    [Trait("Spec", "SPEC-0031:H30-01")]
    [MemberData(nameof(LookAlikes))]
    public void ALookAlikeOfWnsIsRejected(string endpoint)
    {
        var result = PushSubscriptionValidator.Validate(endpoint, P256dh, Auth);

        result.IsFailure.ShouldBeTrue($"endpoint aceito: \"{endpoint}\"");
        result.Error.Code.ShouldBe("notifications.invalid_subscription");
    }

    private static string NewP256dh()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        return Convert.ToBase64String([0x04, .. point.X!, .. point.Y!]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
