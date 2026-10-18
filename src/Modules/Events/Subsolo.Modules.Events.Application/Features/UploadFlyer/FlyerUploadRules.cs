using Subsolo.Common.Application.Media;
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
    /// <summary>Quantos bytes do começo do arquivo bastam para reconhecer o tipo.</summary>
    public const int SignatureLength = 12;

    /// <summary>Tamanho máximo: 10 MB (10 × 1024 × 1024 bytes).</summary>
    public const long MaxBytes = MediaErrors.MaxBytes;

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>O formato de <paramref name="firstBytes"/> (o começo do arquivo) se for aceito e couber em <paramref name="length"/>.</summary>
    public static Result<FlyerFormat> Check(ReadOnlySpan<byte> firstBytes, long length)
    {
        if (Detect(firstBytes) is not { } format)
        {
            return Result.Failure<FlyerFormat>(EventsErrors.FlyerInvalidTypeError());
        }

        return length > MaxBytes
            ? Result.Failure<FlyerFormat>(EventsErrors.FlyerTooLargeError())
            : Result.Success(format);
    }

    private static FlyerFormat? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(JpegSignature))
        {
            return FlyerFormat.Jpeg;
        }

        if (bytes.StartsWith(PngSignature))
        {
            return FlyerFormat.Png;
        }

        return bytes.Length >= SignatureLength && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)
            ? FlyerFormat.WebP
            : null;
    }
}
