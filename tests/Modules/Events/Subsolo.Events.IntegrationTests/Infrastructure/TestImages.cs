using System.Buffers.Binary;
using System.Text;
using NetVips;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0022 — imagens de teste geradas no próprio teste com o NetVips (sem binários no repositório, §7) e leitura dos
/// metadados das variantes. O EXIF com GPS é sintético: um segmento APP1 montado aqui (TIFF little-endian com IFD0
/// <c>Make</c> e o IFD de GPS com latitude e longitude) e inserido no JPEG.
/// </summary>
internal static class TestImages
{
    /// <summary>10 MB do limite do §6 (10 × 1024 × 1024 bytes).</summary>
    public const long TenMegabytes = 10L * 1024 * 1024;

    /// <summary>Texto que vai no campo <c>Make</c> do EXIF sintético (para achar resto de EXIF nas variantes).</summary>
    public const string ExifMake = "SubsoloCam";

    /// <summary>Um JPEG <paramref name="width"/>×<paramref name="height"/> com textura suave (variação por <paramref name="seed"/>).</summary>
    public static byte[] Jpeg(int width, int height, int seed = 1, int quality = 85, double detail = 6)
    {
        using var image = Texture(width, height, seed, detail);
        return image.JpegsaveBuffer(q: quality, keep: Enums.ForeignKeep.None);
    }

    /// <summary><see cref="Jpeg"/> com o EXIF sintético (câmera e GPS) logo depois do SOI.</summary>
    public static byte[] JpegWithGps(int width, int height, int seed = 1)
    {
        var jpeg = WithSegment(Jpeg(width, height, seed), 0xE1, [.. "Exif\0\0"u8, .. ExifTiff()]);
        using var check = Image.NewFromBuffer(jpeg);
        var fields = check.GetFields();
        fields.ShouldContain(field => field.StartsWith("exif-ifd3-GPS", StringComparison.Ordinal),
            $"o JPEG de teste não tem GPS legível (campos: {string.Join(", ", fields)})");
        return jpeg;
    }

    /// <summary>Um PNG em tons de cinza (preto, comprime muito) de <paramref name="width"/>×<paramref name="height"/>.</summary>
    public static byte[] Png(int width, int height)
    {
        using var image = Image.Black(width, height);
        return image.PngsaveBuffer(compression: 9, keep: Enums.ForeignKeep.None);
    }

    /// <summary>Um WebP <paramref name="width"/>×<paramref name="height"/>.</summary>
    public static byte[] WebP(int width, int height, int seed = 1)
    {
        using var image = Texture(width, height, seed, 6);
        return image.WebpsaveBuffer(q: 80, keep: Enums.ForeignKeep.None);
    }

    /// <summary>Um PDF de verdade, pequeno (para mandar como <c>.jpg</c>).</summary>
    public static byte[] Pdf() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [] /Count 0 >> endobj\n" +
        "trailer << /Root 1 0 R >>\n%%EOF\n");

    /// <summary>Começa como JPEG válido e completa com bytes até <paramref name="length"/> (para o limite de tamanho).</summary>
    public static byte[] JpegPaddedTo(long length)
    {
        var head = Jpeg(64, 64);
        var bytes = new byte[length];
        head.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// JPEG com cabeçalho válido (SOI, tabelas, SOF e SOS de um JPEG real de <paramref name="width"/>×<paramref name="height"/>)
    /// e os dados comprimidos trocados por lixo, sem o EOI: as medidas são legíveis, a decodificação não.
    /// </summary>
    public static byte[] CorruptJpeg(int width, int height)
    {
        var jpeg = Jpeg(width, height);
        var dataStart = EntropyDataStart(jpeg);
        var garbage = new byte[16 * 1024];
        new Random(22).NextBytes(garbage);
        return [.. jpeg.AsSpan(0, dataStart), .. garbage];
    }

    /// <summary>Uma cópia de <paramref name="jpeg"/> com um comentário (COM) único: mesmo conteúdo visual, outro hash.</summary>
    public static byte[] WithComment(byte[] jpeg, string comment) =>
        WithSegment(jpeg, 0xFE, Encoding.ASCII.GetBytes(comment));

    /// <summary>Largura e altura lidas pelo libvips.</summary>
    public static (int Width, int Height) Measure(byte[] content)
    {
        using var image = Image.NewFromBuffer(content);
        return (image.Width, image.Height);
    }

    /// <summary>
    /// Metadados que não podem sobrar numa variante: campos EXIF/XMP/ICC lidos pelo libvips e, nos bytes, segmentos APP1
    /// (Exif/XMP) e APP2 (ICC) do JPEG, chunks <c>EXIF</c>/<c>XMP </c>/<c>ICCP</c> do WebP e o texto <see cref="ExifMake"/>.
    /// </summary>
    public static IReadOnlyList<string> Metadata(byte[] content)
    {
        var found = new List<string>();
        using (var image = Image.NewFromBuffer(content))
        {
            found.AddRange(image.GetFields().Where(field =>
                field.StartsWith("exif", StringComparison.Ordinal)
                || field.StartsWith("xmp", StringComparison.Ordinal)
                || field.StartsWith("icc", StringComparison.Ordinal)
                || field.Contains("GPS", StringComparison.OrdinalIgnoreCase)));
        }

        if (content.AsSpan().IndexOf(Encoding.ASCII.GetBytes(ExifMake)) >= 0)
        {
            found.Add($"texto \"{ExifMake}\" do EXIF original");
        }

        if (content.AsSpan().StartsWith("RIFF"u8))
        {
            found.AddRange(WebPChunks(content).Where(chunk => chunk is "EXIF" or "XMP " or "ICCP").Select(chunk => $"chunk WebP {chunk}"));
        }
        else if (content.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            found.AddRange(JpegSegments(content)
                .Where(segment => segment.Marker is 0xE1 or 0xE2)
                .Select(segment => $"segmento JPEG APP{segment.Marker - 0xE0} ({segment.Label})"));
        }

        return found;
    }

    /// <summary>Textura suave (manchas grandes) mais ruído fino de amplitude <paramref name="detail"/>, em sRGB 8 bits.</summary>
    private static Image Texture(int width, int height, int seed, double detail)
    {
        const int Block = 16;
        var coarseWidth = Math.Max(1, width / Block);
        var coarseHeight = Math.Max(1, height / Block);
        using var red = Image.Gaussnoise(coarseWidth, coarseHeight, sigma: 50, mean: 128, seed: seed);
        using var green = Image.Gaussnoise(coarseWidth, coarseHeight, sigma: 50, mean: 128, seed: seed + 1);
        using var blue = Image.Gaussnoise(coarseWidth, coarseHeight, sigma: 50, mean: 128, seed: seed + 2);
        using var coarse = red.Bandjoin(green, blue);
        using var smooth = coarse.Resize((double)width / coarseWidth, kernel: Enums.Kernel.Linear, vscale: (double)height / coarseHeight);
        using var cropped = smooth.Crop(0, 0, width, height);
        using var noise = Image.Gaussnoise(width, height, sigma: detail, mean: 0, seed: seed + 3);
        using var sum = cropped + noise;
        using var bytes = sum.Cast(Enums.BandFormat.Uchar);
        return bytes.Copy(interpretation: Enums.Interpretation.Srgb);
    }

    /// <summary>TIFF do EXIF: IFD0 com <c>Make</c> e o ponteiro para o IFD de GPS (latitude S 23°33'12.34", longitude W 46°38'56.78").</summary>
    private static byte[] ExifTiff()
    {
        var tiff = new byte[152];
        var span = tiff.AsSpan();
        "II"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 8);

        // IFD0 em 8: 2 entradas (Make → 38, GPSInfo → 50), próximo IFD = 0. Make em 38 (11 bytes, com o \0).
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 2);
        Entry(span[10..], tag: 0x010F, type: 2, count: (uint)ExifMake.Length + 1, value: 38);
        Entry(span[22..], tag: 0x8825, type: 4, count: 1, value: 50);
        BinaryPrimitives.WriteUInt32LittleEndian(span[34..], 0);
        Encoding.ASCII.GetBytes(ExifMake).CopyTo(span[38..]);

        // IFD de GPS em 50: 4 entradas, próximo IFD = 0; latitude em 104 e longitude em 128 (3 racionais cada).
        BinaryPrimitives.WriteUInt16LittleEndian(span[50..], 4);
        Entry(span[52..], tag: 0x0001, type: 2, count: 2, value: 'S');
        Entry(span[64..], tag: 0x0002, type: 5, count: 3, value: 104);
        Entry(span[76..], tag: 0x0003, type: 2, count: 2, value: 'W');
        Entry(span[88..], tag: 0x0004, type: 5, count: 3, value: 128);
        BinaryPrimitives.WriteUInt32LittleEndian(span[100..], 0);
        Rationals(span[104..], (23, 1), (33, 1), (1234, 100));
        Rationals(span[128..], (46, 1), (38, 1), (5678, 100));
        return tiff;

        static void Entry(Span<byte> entry, ushort tag, ushort type, uint count, uint value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(entry, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], type);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], count);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], value);
        }

        static void Rationals(Span<byte> target, params (uint Numerator, uint Denominator)[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(target[(i * 8)..], values[i].Numerator);
                BinaryPrimitives.WriteUInt32LittleEndian(target[(i * 8 + 4)..], values[i].Denominator);
            }
        }
    }

    /// <summary>Insere o segmento <paramref name="marker"/> depois do SOI e do APP0 (JFIF), se houver.</summary>
    private static byte[] WithSegment(byte[] jpeg, byte marker, byte[] payload)
    {
        var at = 2;
        if (jpeg[2] == 0xFF && jpeg[3] == 0xE0)
        {
            at = 4 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(4));
        }

        var length = (ushort)(payload.Length + 2);
        byte[] header = [0xFF, marker, (byte)(length >> 8), (byte)length];
        return [.. jpeg.AsSpan(0, at), .. header, .. payload, .. jpeg.AsSpan(at)];
    }

    /// <summary>Segmentos de cabeçalho do JPEG até o SOS: marcador e o começo do conteúdo (Exif, http://ns.adobe.com/xap…, ICC_PROFILE).</summary>
    private static IEnumerable<(byte Marker, string Label)> JpegSegments(byte[] jpeg)
    {
        var at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
        {
            var marker = jpeg[at + 1];
            if (marker == 0xDA)
            {
                yield break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 2));
            var labelLength = Math.Min(12, Math.Max(0, length - 2));
            var label = Encoding.ASCII.GetString(jpeg, at + 4, Math.Min(labelLength, jpeg.Length - at - 4)).TrimEnd('\0');
            yield return (marker, label);
            at += 2 + length;
        }
    }

    private static int EntropyDataStart(byte[] jpeg)
    {
        var at = 2;
        while (at + 4 <= jpeg.Length)
        {
            var marker = jpeg[at + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 2));
            at += 2 + length;
            if (marker == 0xDA)
            {
                return at;
            }
        }

        throw new InvalidOperationException("JPEG de teste sem SOS");
    }

    private static IEnumerable<string> WebPChunks(byte[] webp)
    {
        var at = 12;
        while (at + 8 <= webp.Length)
        {
            var name = Encoding.ASCII.GetString(webp, at, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(at + 4));
            yield return name;
            at += 8 + (int)size + (int)(size % 2);
        }
    }
}
