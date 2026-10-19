using System.Buffers.Binary;

namespace Subsolo.Modules.Events.Application.Features.UploadFlyer;

/// <summary>
/// SPEC-0024 (Emenda v2, b) — as medidas de uma imagem WebP lidas do cabeçalho (a variante <c>full</c> que o
/// processador acabou de gerar), sem decodificar: <c>VP8X</c> (tela), <c>VP8 </c> (com perdas) e <c>VP8L</c> (sem
/// perdas). Cabeçalho não reconhecido → <see langword="null"/> (o flyer fica sem medida e a página usa 3:4).
/// </summary>
internal static class WebPSize
{
    /// <summary>A largura e a altura de <paramref name="webp"/>, ou <see langword="null"/>.</summary>
    public static (int Width, int Height)? Read(ReadOnlySpan<byte> webp)
    {
        if (webp.Length < 30 || !webp.StartsWith("RIFF"u8) || !webp[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var chunk = webp[12..16];
        var data = webp[20..];
        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Largura − 1 e altura − 1 em 24 bits little-endian, depois de 4 bytes de flags.
            return Valid(ReadUInt24(data[4..]) + 1, ReadUInt24(data[7..]) + 1);
        }

        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Quadro-chave: 3 bytes de tag, o código 9D 01 2A e as medidas em 14 bits.
            if (data[3] != 0x9D || data[4] != 0x01 || data[5] != 0x2A)
            {
                return null;
            }

            return Valid(
                BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(data[8..]) & 0x3FFF);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Assinatura 0x2F e as medidas − 1 em 14 bits cada.
            if (data[0] != 0x2F)
            {
                return null;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
            return Valid((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        return null;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private static (int Width, int Height)? Valid(int width, int height) =>
        width > 0 && height > 0 ? (width, height) : null;
}
