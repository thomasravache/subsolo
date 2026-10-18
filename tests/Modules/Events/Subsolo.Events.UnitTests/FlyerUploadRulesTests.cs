using System.Text;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.UploadFlyer;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0022 UT-01 — o validador do upload recebe só os primeiros bytes e o tamanho do arquivo: JPEG, PNG e WebP (pelo
/// conteúdo) até 10 MB passam, com o formato detectado; PDF, GIF e texto dão <c>events.flyer_invalid_type</c> e
/// 10 MB + 1 dá <c>events.flyer_too_large</c>, ambos 400 (Validation). 10 MB = 10 × 1024 × 1024 bytes.
/// </summary>
public sealed class FlyerUploadRulesTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    private const string InvalidType = "events.flyer_invalid_type";

    private const string TooLarge = "events.flyer_too_large";

    /// <summary>Começo de um JPEG (SOI + APP0 JFIF).</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01];

    /// <summary>Assinatura PNG + começo do IHDR.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, (byte)'I', (byte)'H', (byte)'D', (byte)'R'];

    /// <summary><c>RIFF</c> + tamanho + <c>WEBP</c> + <c>VP8 </c>.</summary>
    private static readonly byte[] WebP = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n1 0 obj");

    private static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF89a\u0001\u0000\u0001\u0000");

    private static readonly byte[] Text = Encoding.UTF8.GetBytes("isto não é uma imagem, é só texto com extensão .jpg");

    public static TheoryData<string, FlyerFormat> Accepted => new()
    {
        { nameof(Jpeg), FlyerFormat.Jpeg },
        { nameof(Png), FlyerFormat.Png },
        { nameof(WebP), FlyerFormat.WebP },
    };

    public static TheoryData<string> Rejected => [nameof(Pdf), nameof(Gif), nameof(Text)];

    [Theory]
    [Trait("Spec", "SPEC-0022:UT-01")]
    [MemberData(nameof(Accepted))]
    public void ImageTypesUpTo10MegabytesPassWithTheDetectedFormat(string sample, FlyerFormat expected)
    {
        foreach (var length in new[] { Bytes(sample).Length, 1024L * 1024, TenMegabytes })
        {
            var result = FlyerUploadRules.Check(Bytes(sample), length);

            result.IsSuccess.ShouldBeTrue($"{sample} com {length} bytes: {result.Error.Code}");
            result.Value.ShouldBe(expected, $"{sample} com {length} bytes");
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0022:UT-01")]
    [MemberData(nameof(Rejected))]
    public void OtherContentFailsWithInvalidTypeWhateverTheSize(string sample)
    {
        var result = FlyerUploadRules.Check(Bytes(sample), 4096);

        result.IsFailure.ShouldBeTrue($"{sample} passou como imagem");
        result.Error.Code.ShouldBe(InvalidType, sample);
        result.Error.Type.ShouldBe(ErrorType.Validation, "tipo inválido é 400");
    }

    [Theory]
    [Trait("Spec", "SPEC-0022:UT-01")]
    [MemberData(nameof(Accepted))]
    public void AnImageOf10MegabytesPlusOneFailsWithTooLarge(string sample, FlyerFormat format)
    {
        var result = FlyerUploadRules.Check(Bytes(sample), TenMegabytes + 1);

        result.IsFailure.ShouldBeTrue($"{format} com 10 MB + 1 passou");
        result.Error.Code.ShouldBe(TooLarge, sample);
        result.Error.Type.ShouldBe(ErrorType.Validation, "arquivo grande demais é 400");
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:UT-01")]
    public void TheTypeComesFromTheContentNotFromAJpegLookingPrefix()
    {
        // Só o começo "FF D8" sem o terceiro byte do marcador, e um RIFF que não é WEBP (WAV).
        byte[] truncatedJpeg = [0xFF, 0xD8];
        byte[] wav = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8];

        FlyerUploadRules.Check(truncatedJpeg, 2).Error.Code.ShouldBe(InvalidType, "dois bytes não bastam para ser JPEG");
        FlyerUploadRules.Check(wav, 4096).Error.Code.ShouldBe(InvalidType, "RIFF de áudio não é WebP");
        FlyerUploadRules.Check([], 0).Error.Code.ShouldBe(InvalidType, "arquivo vazio não é imagem");
    }

    private static byte[] Bytes(string sample) => sample switch
    {
        nameof(Jpeg) => Jpeg,
        nameof(Png) => Png,
        nameof(WebP) => WebP,
        nameof(Pdf) => Pdf,
        nameof(Gif) => Gif,
        nameof(Text) => Text,
        _ => throw new ArgumentOutOfRangeException(nameof(sample), sample, "amostra desconhecida"),
    };
}
