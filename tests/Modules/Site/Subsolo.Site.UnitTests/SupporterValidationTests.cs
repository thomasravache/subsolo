using FluentValidation.TestHelper;
using Subsolo.Modules.Site.Application.Features.AddSupporter;

namespace Subsolo.Site.UnitTests;

/// <summary>
/// SPEC-0028 UT-02 — o validador do apoiador recebe nome de 1 e de 61 caracteres, link <c>http://x</c> e
/// <c>https://zine.example</c>: só o par nome válido (2 a 60) + link <c>https</c> é aceito; o resto falha com
/// <c>site.invalid_supporter</c> no campo certo. O link é opcional (nulo passa).
/// </summary>
public sealed class SupporterValidationTests
{
    private const string InvalidSupporter = "site.invalid_supporter";

    private const string HttpsLink = "https://zine.example";

    [Theory]
    [Trait("Spec", "SPEC-0028:UT-02")]
    [InlineData(1)]
    [InlineData(61)]
    public void NameOutside2To60CharactersFailsWithInvalidSupporter(int length)
    {
        var result = new AddSupporterCommandValidator().TestValidate(new AddSupporterCommand(new string('a', length), HttpsLink));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidSupporter);
        result.ShouldNotHaveValidationErrorFor(command => command.Url);
    }

    [Theory]
    [Trait("Spec", "SPEC-0028:UT-02")]
    [InlineData("http://x")]
    [InlineData("http://zine.example")]
    [InlineData("ftp://zine.example")]
    [InlineData("zine.example")]
    [InlineData("javascript:alert(1)")]
    public void LinkThatIsNotHttpsFailsWithInvalidSupporter(string url)
    {
        var result = new AddSupporterCommandValidator().TestValidate(new AddSupporterCommand("Putridearth Zine", url));

        result.ShouldHaveValidationErrorFor(command => command.Url).WithErrorCode(InvalidSupporter);
        result.ShouldNotHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    [Trait("Spec", "SPEC-0028:UT-02")]
    public void TheSpecCasesOnlyAcceptTheValidNameWithTheHttpsLink()
    {
        var validator = new AddSupporterCommandValidator();
        var cases = new[]
        {
            (Name: new string('a', 1), Url: "http://x"),
            (Name: new string('a', 1), Url: HttpsLink),
            (Name: new string('a', 61), Url: "http://x"),
            (Name: new string('a', 61), Url: HttpsLink),
            (Name: "Putridearth Zine", Url: "http://x"),
            (Name: "Putridearth Zine", Url: HttpsLink),
        };

        var accepted = cases
            .Where(candidate => validator.TestValidate(new AddSupporterCommand(candidate.Name, candidate.Url)).IsValid)
            .ToList();

        accepted.ShouldBe([("Putridearth Zine", HttpsLink)]);
    }

    // Guarda: o validador ainda vazio já aceita estes; continua passando depois das regras.
    [Theory]
    [Trait("Spec", "SPEC-0028:UT-02")]
    [InlineData(2, HttpsLink)]
    [InlineData(60, HttpsLink)]
    [InlineData(2, null)]
    public void NamesWith2To60CharactersAndAnHttpsOrNoLinkPass(int length, string? url)
    {
        new AddSupporterCommandValidator().TestValidate(new AddSupporterCommand(new string('a', length), url))
            .ShouldNotHaveAnyValidationErrors();
    }
}
