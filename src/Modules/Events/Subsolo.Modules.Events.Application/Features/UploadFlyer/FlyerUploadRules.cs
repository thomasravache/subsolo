using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application.Features.UploadFlyer;

/// <summary>SPEC-0022 — formatos de flyer aceitos (detectados pelo conteúdo, nunca pela extensão).</summary>
public enum FlyerFormat
{
    Jpeg,
    Png,
    WebP,
}

/// <summary>
/// SPEC-0022 (UT-01) — validação do upload antes de ler o arquivo inteiro: o tipo pelos primeiros bytes e o tamanho
/// declarado, com os códigos <c>events.flyer_invalid_type</c> e <c>events.flyer_too_large</c> do §6.
/// </summary>
public static class FlyerUploadRules
{
    /// <summary>O formato de <paramref name="firstBytes"/> (o começo do arquivo) se for aceito e couber em <paramref name="length"/>.</summary>
    public static Result<FlyerFormat> Check(ReadOnlySpan<byte> firstBytes, long length) =>
        throw new NotImplementedException(
            $"SPEC-0022: validação do upload ainda não implementada ({firstBytes.Length} bytes, {length}).");
}
