using NetVips;

namespace Subsolo.ArchitectureTests.ModularMonolith.MediaSamples.Shows.Infrastructure;

/// <summary>
/// SPEC-0022 IT-08 — dublê: um módulo fora do Common.Infrastructure que redimensiona o flyer direto com o NetVips
/// (viola o ADR-0015). Só é avaliado pelo teste da violação, nunca pelas regras dos módulos reais.
/// </summary>
public static class DirectFlyerResizer
{
    public static Image Thumb(Image flyer)
    {
        ArgumentNullException.ThrowIfNull(flyer);
        return flyer.ThumbnailImage(400);
    }
}
