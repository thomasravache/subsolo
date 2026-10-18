using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0016 UT-01 — com o dublê de <c>INotificationsApi</c> e o <c>FakeTimeProvider</c>: <c>RequestMagicLink</c> para
/// <c>fulano@email.com</c> grava só o hash do token (nunca o token), com validade de 15 min, e manda o modelo
/// <c>MagicLink</c> com o link <c>{App:PublicUrl}/entrar/confirmar?token=…</c> e <c>minutes=15</c>; e-mail inválido →
/// <c>users.invalid_email</c> sem chamar o dublê; <c>ConsumeMagicLink</c> com o token válido entra pelo
/// <c>ISignInService</c> e marca o token como usado; usado, expirado (16 min) ou aleatório →
/// <c>users.magic_link_invalid</c>.
/// </summary>
public sealed class MagicLinkTests
{
    private const string Email = "fulano@email.com";

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task RequestStoresOnlyTheHashAndSendsTheMagicLinkTemplateFor15Minutes()
    {
        var harness = new MagicLinkHarness();

        var result = await harness.RequestAsync(Email);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var request = harness.Notifications.Requests.ShouldHaveSingleItem("o pedido do link manda um e-mail");
        request.To.ShouldBe(Email);
        request.Template.ShouldBe(EmailTemplate.MagicLink);
        request.Data["minutes"].ShouldBe("15");
        var token = MagicLinkHarness.TokenOf(request);
        MagicLinkTokens.ShouldBe256Bits(token);

        var stored = harness.Tokens.All.ShouldHaveSingleItem("o pedido grava um link");
        MagicLinkTokens.ShouldBeHashOf(stored.TokenHash, token);
        stored.Email.ShouldBe(Email);
        stored.CreatedAtUtc.ShouldBe(MagicLinkHarness.Start);
        stored.ExpiresAtUtc.ShouldBe(MagicLinkHarness.Start + TimeSpan.FromMinutes(15));
        stored.UsedAtUtc.ShouldBeNull();
        harness.UnitOfWork.Saves.ShouldBeGreaterThan(0, "o link não foi gravado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task RequestNormalizesTheEmailBeforeStoringAndSending()
    {
        var harness = new MagicLinkHarness();

        await harness.RequestTokenAsync(" Fulano@Email.COM ");

        harness.Tokens.All.ShouldHaveSingleItem().Email.ShouldBe(Email);
        harness.Notifications.Requests.ShouldHaveSingleItem().To.ShouldBe(Email);
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task EachRequestIssuesADifferentToken()
    {
        var harness = new MagicLinkHarness();

        var first = await harness.RequestTokenAsync(Email);
        var second = await harness.RequestTokenAsync(Email);

        first.ShouldNotBe(second, "cada pedido gera um token novo e aleatório");
        harness.Tokens.All.Count.ShouldBe(2);
    }

    /// <summary>E-mails inválidos: sem arroba e com 255 caracteres (242 letras + "@subsolo.test").</summary>
    public static TheoryData<string> InvalidEmails => new()
    {
        "sem-arroba",
        new string('a', 242) + "@subsolo.test",
    };

    [Theory]
    [Trait("Spec", "SPEC-0016:UT-01")]
    [MemberData(nameof(InvalidEmails))]
    public async Task RequestWithAnInvalidEmailFailsWithoutCallingNotifications(string email)
    {
        var harness = new MagicLinkHarness();

        var result = await harness.RequestAsync(email);

        result.IsFailure.ShouldBeTrue("e-mail inválido não pede link");
        result.Error.Code.ShouldBe("users.invalid_email");
        harness.Notifications.Requests.ShouldBeEmpty("e-mail inválido não chama o Notifications");
        harness.Tokens.All.ShouldBeEmpty("e-mail inválido não grava link");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task ConsumeWithAValidTokenSignsInByMagicLinkAndMarksTheTokenUsed()
    {
        var harness = new MagicLinkHarness();
        var token = await harness.RequestTokenAsync(Email, "/curitiba");
        harness.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await harness.ConsumeAsync(token);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.ReturnTo.ShouldBe("/curitiba");
        harness.SignIn.SignIns.ShouldHaveSingleItem().ShouldBe((Email, SignInMethod.MagicLink));
        harness.Tokens.All.ShouldHaveSingleItem().UsedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task ConsumeWithoutReturnToGoesBackToTheHomePage()
    {
        var harness = new MagicLinkHarness();
        var token = await harness.RequestTokenAsync(Email);

        var result = await harness.ConsumeAsync(token);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.ReturnTo.ShouldBe("/");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task ConsumingTheSameTokenTwiceFailsWithMagicLinkInvalid()
    {
        var harness = new MagicLinkHarness();
        var token = await harness.RequestTokenAsync(Email);
        (await harness.ConsumeAsync(token)).IsSuccess.ShouldBeTrue("o primeiro consumo entra");

        var second = await harness.ConsumeAsync(token);

        ShouldBeMagicLinkInvalid(second);
        harness.SignIn.SignIns.Count.ShouldBe(1, "o link usado não entra de novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task ConsumingAnExpiredTokenFailsWithMagicLinkInvalid()
    {
        var harness = new MagicLinkHarness();
        var token = await harness.RequestTokenAsync(Email);
        harness.Clock.Advance(TimeSpan.FromMinutes(16));

        var result = await harness.ConsumeAsync(token);

        ShouldBeMagicLinkInvalid(result);
        harness.SignIn.SignIns.ShouldBeEmpty("link expirado não entra");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public async Task ConsumingARandomTokenFailsWithMagicLinkInvalid()
    {
        var harness = new MagicLinkHarness();
        await harness.RequestTokenAsync(Email);

        var result = await harness.ConsumeAsync(SessionTokens.NewRandom());

        ShouldBeMagicLinkInvalid(result);
        harness.SignIn.SignIns.ShouldBeEmpty("token aleatório não entra");
        harness.Tokens.All.ShouldHaveSingleItem().UsedAtUtc.ShouldBeNull("o link de verdade continua sem uso");
    }

    private static void ShouldBeMagicLinkInvalid(Subsolo.Common.Domain.Result result)
    {
        result.IsFailure.ShouldBeTrue("o consumo deveria falhar");
        result.Error.Code.ShouldBe("users.magic_link_invalid");
    }
}
