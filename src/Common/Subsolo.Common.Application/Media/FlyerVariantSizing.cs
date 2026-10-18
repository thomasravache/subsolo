namespace Subsolo.Common.Application.Media;

/// <summary>SPEC-0022 — largura e altura em pixels.</summary>
public readonly record struct PixelSize(int Width, int Height);

/// <summary>
/// SPEC-0022 — medidas das variantes de um flyer: <see cref="Thumb"/> e <see cref="Full"/>, a tela do <c>og</c>
/// (<see cref="OgCanvas"/>) e o flyer inteiro dentro dela (<see cref="OgFlyer"/>, contido, sem corte).
/// </summary>
public sealed record FlyerVariantSizes(PixelSize Thumb, PixelSize Full, PixelSize OgCanvas, PixelSize OgFlyer);

/// <summary>SPEC-0022 (UT-02) — cálculo das medidas das variantes do ADR-0015.</summary>
public static class FlyerVariantSizing
{
    /// <summary>Largura do <c>thumb</c> (nunca aumenta imagem mais estreita).</summary>
    public const int ThumbWidth = 400;

    /// <summary>Largura do <c>full</c> (nunca aumenta imagem mais estreita).</summary>
    public const int FullWidth = 1080;

    /// <summary>Tela do <c>og</c> (Open Graph): sempre 1200×630.</summary>
    public static readonly PixelSize OgCanvas = new(1200, 630);

    /// <summary>Medidas das variantes para um flyer de <paramref name="original"/>.</summary>
    public static FlyerVariantSizes For(PixelSize original)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(original.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(original.Height);

        var ogScale = Math.Min((double)OgCanvas.Width / original.Width, (double)OgCanvas.Height / original.Height);
        var ogFlyer = new PixelSize(
            Math.Clamp((int)Math.Round(original.Width * ogScale), 1, OgCanvas.Width),
            Math.Clamp((int)Math.Round(original.Height * ogScale), 1, OgCanvas.Height));

        return new FlyerVariantSizes(
            ToWidth(original, ThumbWidth),
            ToWidth(original, FullWidth),
            OgCanvas,
            ogFlyer);
    }

    /// <summary>Na largura <paramref name="width"/> (ou na original, se for menor), mantendo a proporção.</summary>
    private static PixelSize ToWidth(PixelSize original, int width)
    {
        var target = Math.Min(width, original.Width);
        var height = Math.Max(1, (int)Math.Round((double)original.Height * target / original.Width));
        return new PixelSize(target, height);
    }
}
