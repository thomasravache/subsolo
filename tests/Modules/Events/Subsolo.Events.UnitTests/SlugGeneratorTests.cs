using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Slugs;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0017 UT-01 — o gerador de slug tira acentos, passa para minúsculas, troca espaços por hífen e descarta o que
/// não cabe em <c>^[a-z0-9-]{2,60}$</c> ("São José dos Pinhais" → <c>sao-jose-dos-pinhais</c>, "  Curitiba " →
/// <c>curitiba</c>, "Foz do Iguaçu!" → <c>foz-do-iguacu</c>); <c>City.Update</c> troca nome, UF e fuso e mantém o slug.
/// </summary>
public sealed class SlugGeneratorTests
{
    [Theory]
    [Trait("Spec", "SPEC-0017:UT-01")]
    [InlineData("São José dos Pinhais", "sao-jose-dos-pinhais")]
    [InlineData("  Curitiba ", "curitiba")]
    [InlineData("Foz do Iguaçu!", "foz-do-iguacu")]
    [InlineData("Crust", "crust")]
    [InlineData("Crust Punk", "crust-punk")]
    public void GeneratesTheSlugFromTheName(string name, string expected)
    {
        SlugGenerator.FromName(name).ShouldBe(expected);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-01")]
    [InlineData("São José dos Pinhais")]
    [InlineData("  Curitiba ")]
    [InlineData("Foz do Iguaçu!")]
    public void GeneratedSlugMatchesTheSlugPattern(string name)
    {
        SlugGenerator.FromName(name).ShouldMatch(SlugGenerator.Pattern);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:UT-01")]
    public void UpdateChangesNameStateAndTimeZoneButKeepsTheSlug()
    {
        var city = City.Create("sao-jose-dos-pinhais", "São José dos Pinhais", "PR", "America/Sao_Paulo");

        city.Update("São José", "SC", "America/Manaus");

        city.Slug.ShouldBe("sao-jose-dos-pinhais");
        city.Name.ShouldBe("São José");
        city.State.ShouldBe("SC");
        city.TimeZone.ShouldBe("America/Manaus");
    }
}
