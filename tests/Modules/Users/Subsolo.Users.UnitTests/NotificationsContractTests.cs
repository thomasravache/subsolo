using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0016 CT-01 — o dublê de <see cref="INotificationsApi"/> dos unitários (<see cref="RecordingNotificationsApi"/>)
/// segue o contrato SPEC-0018@1: assinatura de <c>SendEmailAsync</c>, o modelo <see cref="EmailTemplate.MagicLink"/>,
/// as chaves <c>link</c> e <c>minutes</c> e os códigos de erro; e o pedido que o <c>RequestMagicLink</c> manda cumpre
/// as regras do modelo (destinatário válido, link absoluto https, <c>minutes</c> inteiro de 1 a 60). O IT-01 confirma
/// contra o módulo real com o Mailpit.
/// </summary>
public sealed class NotificationsContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0016:CT-01")]
    public void SendEmailAsyncHasTheContractSignature()
    {
        var method = typeof(INotificationsApi).GetMethod(nameof(INotificationsApi.SendEmailAsync)).ShouldNotBeNull();

        method.ReturnType.ShouldBe(typeof(Task<Result>));
        method.GetParameters().Select(parameter => parameter.ParameterType)
            .ShouldBe([typeof(EmailRequest), typeof(CancellationToken)]);
        typeof(INotificationsApi).IsAssignableFrom(typeof(RecordingNotificationsApi)).ShouldBeTrue();
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:CT-01")]
    public void EmailRequestAndTemplateMatchTheContract()
    {
        typeof(EmailRequest).GetProperty(nameof(EmailRequest.To))!.PropertyType.ShouldBe(typeof(string));
        typeof(EmailRequest).GetProperty(nameof(EmailRequest.Template))!.PropertyType.ShouldBe(typeof(EmailTemplate));
        typeof(EmailRequest).GetProperty(nameof(EmailRequest.Data))!.PropertyType
            .ShouldBe(typeof(IReadOnlyDictionary<string, string>));
        Enum.GetNames<EmailTemplate>().ShouldContain(nameof(EmailTemplate.MagicLink));
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:CT-01")]
    public void ErrorCodesMatchTheContract()
    {
        NotificationsErrors.InvalidRecipient.ShouldBe("notifications.invalid_recipient");
        NotificationsErrors.InvalidData.ShouldBe("notifications.invalid_data");
        NotificationsErrors.EmailUnavailable.ShouldBe("notifications.email_unavailable");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:CT-01")]
    public async Task TheMagicLinkRequestSatisfiesTheTemplateRules()
    {
        var harness = new MagicLinkHarness();

        await harness.RequestTokenAsync("contrato@subsolo.test");

        var request = harness.Notifications.Requests.ShouldHaveSingleItem();
        request.Template.ShouldBe(EmailTemplate.MagicLink);
        request.To.ShouldBe("contrato@subsolo.test");
        request.Data.Keys.ShouldBe(["link", "minutes"], ignoreOrder: true, "o modelo MagicLink leva só link e minutes");
        Uri.TryCreate(request.Data["link"], UriKind.Absolute, out var link).ShouldBeTrue("link não é URL absoluta");
        link.Scheme.ShouldBe(Uri.UriSchemeHttps);
        int.TryParse(request.Data["minutes"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minutes)
            .ShouldBeTrue("minutes não é inteiro");
        minutes.ShouldBeInRange(1, 60);
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:CT-01")]
    public async Task EmailUnavailableFromNotificationsIsReturnedToTheCaller()
    {
        var harness = new MagicLinkHarness();
        harness.Notifications.FailWith = NotificationsErrors.EmailUnavailable;

        var result = await harness.RequestAsync("smtp.fora@subsolo.test");

        result.IsFailure.ShouldBeTrue("SMTP fora do ar não é sucesso");
        result.Error.Code.ShouldBe(NotificationsErrors.EmailUnavailable);
    }
}
