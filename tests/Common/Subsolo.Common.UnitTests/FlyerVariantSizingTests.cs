using Subsolo.Common.Application.Media;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0022 UT-02 (ADR-0015) — medidas das variantes: <c>thumb</c> com 400 px de largura e <c>full</c> com 1080 px
/// (sem aumentar imagem mais estreita), ambos na proporção do original (arredondamento de até 1 px); <c>og</c> sempre
/// numa tela de 1200×630 com o flyer inteiro dentro dela — contido (encosta em um dos lados, sem passar de nenhum) e
/// na proporção do original, sem corte.
/// </summary>
public sealed class FlyerVariantSizingTests
{
    private static readonly PixelSize OgCanvas = new(1200, 630);

    public static TheoryData<int, int, int, int, int, int, int, int> Flyers => new()
    {
        // original      thumb        full         og (flyer dentro da tela 1200×630)
        { 1080, 1920, 400, 711, 1080, 1920, 354, 630 },
        { 4000, 6000, 400, 600, 1080, 1620, 420, 630 },
        { 1200, 600, 400, 200, 1080, 540, 1200, 600 },
    };

    [Theory]
    [Trait("Spec", "SPEC-0022:UT-02")]
    [MemberData(nameof(Flyers))]
    public void VariantsHaveTheWidthsOfTheAdrAndTheOgContainsTheWholeFlyer(
        int width, int height, int thumbWidth, int thumbHeight, int fullWidth, int fullHeight, int ogWidth, int ogHeight)
    {
        var sizes = FlyerVariantSizing.For(new PixelSize(width, height));
        var where = $"{width}×{height}";

        sizes.Thumb.Width.ShouldBe(thumbWidth, $"{where} thumb");
        sizes.Thumb.Height.ShouldBeInRange(thumbHeight - 1, thumbHeight + 1, $"{where} thumb");
        sizes.Full.Width.ShouldBe(fullWidth, $"{where} full");
        sizes.Full.Height.ShouldBeInRange(fullHeight - 1, fullHeight + 1, $"{where} full");
        sizes.OgCanvas.ShouldBe(OgCanvas, $"{where} og");
        sizes.OgFlyer.Width.ShouldBeInRange(ogWidth - 1, ogWidth + 1, $"{where} flyer dentro do og");
        sizes.OgFlyer.Height.ShouldBeInRange(ogHeight - 1, ogHeight + 1, $"{where} flyer dentro do og");
        ShouldBeContained(sizes.OgFlyer, width, height, where);
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:UT-02")]
    public void FullDoesNotEnlargeAFlyerNarrowerThan1080()
    {
        var sizes = FlyerVariantSizing.For(new PixelSize(600, 900));

        sizes.Full.ShouldBe(new PixelSize(600, 900), "o full não aumenta imagem menor que 1080 px");
        sizes.Thumb.Width.ShouldBe(400);
        sizes.Thumb.Height.ShouldBeInRange(599, 601);
        sizes.OgCanvas.ShouldBe(OgCanvas);
        ShouldBeContained(sizes.OgFlyer, 600, 900, "600×900");
    }

    /// <summary>Dentro da tela, encostando em um dos lados, com a proporção do original (sem corte nem distorção).</summary>
    private static void ShouldBeContained(PixelSize flyer, int width, int height, string where)
    {
        flyer.Width.ShouldBeLessThanOrEqualTo(OgCanvas.Width, $"{where}: o flyer passa da largura do og (corte)");
        flyer.Height.ShouldBeLessThanOrEqualTo(OgCanvas.Height, $"{where}: o flyer passa da altura do og (corte)");
        (flyer.Width == OgCanvas.Width || flyer.Height == OgCanvas.Height)
            .ShouldBeTrue($"{where}: o flyer {flyer} não encosta em nenhum lado da tela do og (não está contido)");
        var expectedRatio = (double)width / height;
        var actualRatio = (double)flyer.Width / flyer.Height;
        Math.Abs(actualRatio - expectedRatio).ShouldBeLessThan(
            expectedRatio * 0.01, $"{where}: o flyer dentro do og mudou de proporção ({flyer})");
    }
}
