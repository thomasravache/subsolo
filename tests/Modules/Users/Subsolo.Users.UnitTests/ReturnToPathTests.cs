using Subsolo.Modules.Users.Application.Features.RequestMagicLink;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0016 UT-03 — saneamento do <c>returnTo</c> (sem redirecionamento aberto): só caminho relativo do próprio site
/// é mantido (<c>/curitiba</c>); <c>//evil.com</c>, <c>https://evil.com</c>, <c>/\evil.com</c>,
/// <c>javascript:alert(1)</c>, vazio e nulo viram <c>/</c>.
/// </summary>
public sealed class ReturnToPathTests
{
    [Theory]
    [Trait("Spec", "SPEC-0016:UT-03")]
    [InlineData("/curitiba")]
    [InlineData("/conta")]
    [InlineData("/")]
    public void RelativePathOfTheSiteIsKept(string returnTo) =>
        ReturnToPath.Sanitize(returnTo).ShouldBe(returnTo);

    [Theory]
    [Trait("Spec", "SPEC-0016:UT-03")]
    [InlineData("//evil.com")]
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com/curitiba")]
    [InlineData("/\\evil.com")]
    [InlineData("/\\\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("curitiba")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnythingElseBecomesTheHomePage(string? returnTo) =>
        ReturnToPath.Sanitize(returnTo).ShouldBe("/", $"returnTo \"{returnTo}\" não é caminho relativo seguro");
}
