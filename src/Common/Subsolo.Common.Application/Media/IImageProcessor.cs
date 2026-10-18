using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — processamento do flyer: confere o tipo pelo conteúdo e os limites, remove os metadados e gera
/// as variantes <c>thumb</c>, <c>full</c> (WebP) e <c>og</c> (JPEG 1200×630).
/// </summary>
public interface IImageProcessor
{
    Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct);
}
