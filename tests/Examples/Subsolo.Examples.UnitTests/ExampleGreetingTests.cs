using Subsolo.Examples.Sample;

namespace Subsolo.Examples.UnitTests;

/// <summary>
/// SPEC-0005 UT-01 — o projeto de testes unitários de exemplo roda no <c>dotnet test</c> e o teste carrega a tag de
/// rastreabilidade <c>[Trait("Spec", "SPEC-0005:UT-01")]</c>, visível para o xUnit (e, portanto, para filtros e
/// relatórios).
/// </summary>
public sealed class ExampleGreetingTests
{
    private const string SpecTag = "SPEC-0005:UT-01";

    [Fact]
    [Trait("Spec", SpecTag)]
    public void GreetingNamesTheGivenPlaceAndTestIsListedWithItsSpecTag()
    {
        ExampleGreeting.For("Subsolo").ShouldBe("Bem-vindo ao Subsolo");

        var traits = TestContext.Current.Test?.Traits;
        traits.ShouldNotBeNull("o xUnit deveria expor o teste em execução");
        traits.ShouldContainKey("Spec");
        traits["Spec"].ShouldContain(SpecTag);
    }
}
