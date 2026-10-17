using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.UnitTests.Infrastructure;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0018 UT-02 — <c>SendEmailAsync</c> valida antes de qualquer envio: destinatário fora do formato de e-mail, com
/// mais de 254 caracteres ou com quebra de linha (injeção de cabeçalho, §7.6) → <c>notifications.invalid_recipient</c>;
/// <c>link</c> ausente, não absoluto, fora de https (http só em Development) ou <c>minutes</c> fora de 1–60 →
/// <c>notifications.invalid_data</c>; nos dois casos o canal não é chamado e nada é lançado. Um dado com
/// <c>&lt;script&gt;</c> chega ao HTML escapado (como texto), com o link ainda exato.
/// </summary>
public sealed class SendEmailValidationTests
{
    public static TheoryData<string> InvalidRecipients => new()
    {
        "sem-arroba",
        AddressOfLength(255),
        string.Empty,
        "   ",
        "fa@subsolo.test\r\nBcc: alvo@subsolo.test",
        "fa@subsolo.test\nBcc: alvo@subsolo.test",
    };

    [Theory]
    [Trait("Spec", "SPEC-0018:UT-02")]
    [MemberData(nameof(InvalidRecipients))]
    public async Task InvalidRecipientIsRejectedWithoutCallingTheChannel(string to)
    {
        await using var harness = new NotificationsHarness();

        var result = await harness.SendAsync(NotificationsHarness.MagicLink(to: to));

        result.IsFailure.ShouldBeTrue($"destinatário aceito: \"{to}\" ({to.Length} caracteres)");
        result.Error.Code.ShouldBe(NotificationsErrors.InvalidRecipient);
        harness.Channel.Calls.ShouldBe(0, "o canal foi chamado com um destinatário inválido");
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-02")]
    public async Task RecipientWith254CharactersIsAccepted()
    {
        await using var harness = new NotificationsHarness();
        var to = AddressOfLength(254);

        var email = await harness.RenderAsync(NotificationsHarness.MagicLink(to: to));

        email.Recipient.ShouldBe(to);
    }

    public static TheoryData<string?, string?> InvalidMagicLinkData => new()
    {
        { null, "15" },
        { string.Empty, "15" },
        { "/relativo", "15" },
        { "subsolo.test/entrar/confirmar?token=abc", "15" },
        { "javascript:alert(1)", "15" },
        { "ftp://subsolo.test/entrar/confirmar?token=abc", "15" },
        { NotificationsHarness.Link, null },
        { NotificationsHarness.Link, "0" },
        { NotificationsHarness.Link, "61" },
        { NotificationsHarness.Link, "-5" },
        { NotificationsHarness.Link, "quinze" },
        { NotificationsHarness.Link, "1.5" },
        { NotificationsHarness.Link, string.Empty },
    };

    [Theory]
    [Trait("Spec", "SPEC-0018:UT-02")]
    [MemberData(nameof(InvalidMagicLinkData))]
    public async Task InvalidMagicLinkDataIsRejectedWithoutCallingTheChannel(string? link, string? minutes)
    {
        await using var harness = new NotificationsHarness();
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (link is not null)
        {
            data["link"] = link;
        }

        if (minutes is not null)
        {
            data["minutes"] = minutes;
        }

        var result = await harness.SendAsync(new EmailRequest(NotificationsHarness.Recipient, EmailTemplate.MagicLink, data));

        result.IsFailure.ShouldBeTrue($"dados aceitos: link=\"{link}\", minutes=\"{minutes}\"");
        result.Error.Code.ShouldBe(NotificationsErrors.InvalidData);
        harness.Channel.Calls.ShouldBe(0, "o canal foi chamado com dados inválidos");
    }

    [Theory]
    [Trait("Spec", "SPEC-0018:UT-02")]
    [InlineData("1")]
    [InlineData("60")]
    public async Task MinutesAtTheLimitsAreAccepted(string minutes)
    {
        await using var harness = new NotificationsHarness();

        var email = await harness.RenderAsync(NotificationsHarness.MagicLink(minutes: minutes));

        email.TextBody.ShouldContain($"{minutes} minuto", Case.Sensitive, email.TextBody);
    }

    [Theory]
    [Trait("Spec", "SPEC-0018:UT-02")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task HttpLinkOutsideDevelopmentIsRejected(string environment)
    {
        await using var harness = new NotificationsHarness(environment);

        var result = await harness.SendAsync(NotificationsHarness.MagicLink(link: "http://subsolo.test/entrar/confirmar?token=abc"));

        result.IsFailure.ShouldBeTrue($"link http aceito em {environment}");
        result.Error.Code.ShouldBe(NotificationsErrors.InvalidData);
        harness.Channel.Calls.ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-02")]
    public async Task HttpLinkIsAcceptedInDevelopment()
    {
        await using var harness = new NotificationsHarness("Development");
        const string Link = "http://localhost:3000/entrar/confirmar?token=abc";

        var email = await harness.RenderAsync(NotificationsHarness.MagicLink(link: Link));

        email.TextBody.ShouldContain(Link, Case.Sensitive, email.TextBody);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:UT-02")]
    public async Task ScriptInTheDataIsEscapedInTheHtml()
    {
        await using var harness = new NotificationsHarness();
        const string Link = "https://subsolo.test/entrar/confirmar?token=abc&next=\"><script>alert(1)</script>";

        var email = await harness.RenderAsync(NotificationsHarness.MagicLink(link: Link));

        email.HtmlBody.ShouldNotContain("<script", Case.Insensitive, $"<script> chegou como HTML:\n{email.HtmlBody}");
        email.HtmlBody.ShouldNotContain("\"><", Case.Sensitive, $"o dado fechou o atributo:\n{email.HtmlBody}");
        EmailHtml.AnchorTo(email.HtmlBody, Link).ShouldNotBeNull($"o botão não leva ao link exato (escapado):\n{email.HtmlBody}");
        email.TextBody.ShouldContain(Link, Case.Sensitive, "o texto puro leva o link exato");
    }

    /// <summary>
    /// Endereço válido com exatamente <paramref name="length"/> caracteres: <c>fa@</c> e rótulos de até 63 caracteres
    /// antes de <c>subsolo.test</c> (o tamanho total é o que importa, não um rótulo ou local longo demais).
    /// </summary>
    private static string AddressOfLength(int length)
    {
        const string Local = "fa@";
        const string Suffix = "subsolo.test";
        var labels = new List<string>();
        var remaining = length - Local.Length - Suffix.Length;
        while (remaining > 0)
        {
            var label = Math.Min(63, remaining - 1);
            labels.Add(new string('a', label));
            remaining -= label + 1;
        }

        var address = Local + string.Concat(labels.Select(label => label + ".")) + Suffix;
        address.Length.ShouldBe(length);
        return address;
    }
}
