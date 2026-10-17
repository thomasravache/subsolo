using System.Text.RegularExpressions;
using Subsolo.Notifications.UnitTests.Infrastructure;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0018 UT-01 — o modelo <c>MagicLink</c> com <c>link=https://subsolo.test/entrar/confirmar?token=abc</c> e
/// <c>minutes=15</c>, renderizado pelo <c>SendEmailAsync</c> (o e-mail é lido no dublê do canal), tem o assunto do §6,
/// HTML em pt-BR com o botão "Entrar no Subsolo" (texto, não imagem) para o link exato, texto puro com o link exato,
/// as frases do §6 nas duas partes e o botão com contraste AA (texto branco sobre #D7263D, ≥ 4,5:1). As cores do botão
/// ficam inline no <c>&lt;a&gt;</c>, porque os clientes de e-mail ignoram folhas de estilo.
/// </summary>
public sealed partial class MagicLinkTemplateTests
{
    private const string Subject = "Seu link para entrar no Subsolo";
    private const string Button = "Entrar no Subsolo";
    private const string Validity = "O link vale por 15 minutos e só funciona uma vez.";
    private const string NotYou = "Se não foi você, ignore este e-mail.";

    /// <summary>WCAG 2.x AA para texto normal.</summary>
    private const double MinimumContrast = 4.5;

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-01")]
    public async Task MagicLinkHasTheSubjectTheRecipientAndBothParts()
    {
        await using var harness = new NotificationsHarness();

        var email = await harness.RenderAsync(NotificationsHarness.MagicLink());

        email.Recipient.ShouldBe(NotificationsHarness.Recipient);
        email.Subject.ShouldBe(Subject);
        email.HtmlBody.ShouldNotBeNullOrWhiteSpace("sem parte HTML");
        email.TextBody.ShouldNotBeNullOrWhiteSpace("sem parte em texto puro");
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-01")]
    public async Task HtmlIsInPortugueseWithTheTextButtonToTheExactLinkAndThePhrases()
    {
        await using var harness = new NotificationsHarness();

        var html = (await harness.RenderAsync(NotificationsHarness.MagicLink())).HtmlBody;

        HtmlLang().IsMatch(html).ShouldBeTrue($"o <html> não declara lang=\"pt-BR\":\n{html}");
        var button = EmailHtml.AnchorTo(html, NotificationsHarness.Link)
            .ShouldNotBeNull($"nenhum <a href> com o link exato {NotificationsHarness.Link}:\n{html}");
        EmailHtml.VisibleText(button.Inner).ShouldBe(Button, $"texto do botão: {button.Inner}");
        button.Inner.ShouldNotContain("<img", Case.Insensitive, "o botão é texto, não imagem");

        var text = EmailHtml.VisibleText(html);
        text.ShouldContain(Validity, Case.Sensitive, text);
        text.ShouldContain(NotYou, Case.Sensitive, text);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-01")]
    public async Task TextPartHasTheExactLinkAndThePhrases()
    {
        await using var harness = new NotificationsHarness();

        var text = (await harness.RenderAsync(NotificationsHarness.MagicLink())).TextBody;

        text.ShouldContain(NotificationsHarness.Link, Case.Sensitive, text);
        text.ShouldContain(Validity, Case.Sensitive, text);
        text.ShouldContain(NotYou, Case.Sensitive, text);
        text.ShouldNotContain("<a ", Case.Insensitive, "a parte em texto puro não tem HTML");
        text.ShouldNotContain("<html", Case.Insensitive, "a parte em texto puro não tem HTML");
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-01")]
    public async Task ButtonColorsMeetAaContrast()
    {
        await using var harness = new NotificationsHarness();

        var html = (await harness.RenderAsync(NotificationsHarness.MagicLink())).HtmlBody;

        var button = EmailHtml.AnchorTo(html, NotificationsHarness.Link)
            .ShouldNotBeNull($"nenhum <a href> com o link exato {NotificationsHarness.Link}:\n{html}");
        var style = EmailHtml.InlineStyle(button.Attributes);
        var foreground = EmailHtml.HexColor(style.GetValueOrDefault("color"))
            .ShouldNotBeNull($"o botão não declara color inline: {button.Attributes}");
        var background = EmailHtml.HexColor(style.GetValueOrDefault("background-color") ?? style.GetValueOrDefault("background"))
            .ShouldNotBeNull($"o botão não declara background-color inline: {button.Attributes}");

        EmailHtml.ContrastRatio(foreground, background).ShouldBeGreaterThanOrEqualTo(
            MinimumContrast, $"contraste do botão abaixo de AA: {button.Attributes}");
    }

    [GeneratedRegex("""<html\b[^>]*\blang\s*=\s*["']pt-BR["']""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLang();
}
