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
    /// <summary>Medidas das variantes para um flyer de <paramref name="original"/>.</summary>
    public static FlyerVariantSizes For(PixelSize original) =>
        throw new NotImplementedException($"SPEC-0022: medidas das variantes ainda não implementadas ({original}).");
}
