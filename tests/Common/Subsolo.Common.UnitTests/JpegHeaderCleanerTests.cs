using Subsolo.Common.Infrastructure.Media;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0024 H23-03 (revisão da SPEC-0023) — o <see cref="JpegHeaderCleaner"/>:
/// <list type="bullet">
/// <item>só aloca a cópia quando acha o primeiro byte solto: um JPEG sem bytes soltos volta como está, sem alocar uma
/// cópia do tamanho do arquivo;</item>
/// <item>com entrada malformada — comprimento de segmento truncado, <c>FF 00</c> no cabeçalho (só vale dentro dos dados
/// comprimidos), sem SOS — devolve os bytes originais (o decodificador decide), mesmo que antes tenha visto um byte
/// solto;</item>
/// <item>com um byte solto entre dois segmentos de um JPEG bem formado, tira só esse byte (o comportamento da
/// SPEC-0023, mantido).</item>
/// </list>
/// </summary>
public sealed class JpegHeaderCleanerTests
{
    private static readonly byte[] StartOfImage = [0xFF, 0xD8];

    /// <summary>APP0 (JFIF) com o comprimento 16 (2 + 14 bytes).</summary>
    private static readonly byte[] App0 =
        [0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00];

    /// <summary>SOS com o comprimento 8 (2 + 6 bytes).</summary>
    private static readonly byte[] StartOfScan = [0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00];

    private static readonly byte[] EndOfImage = [0xFF, 0xD9];

    private static byte[] Jpeg(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>Dados comprimidos de <paramref name="length"/> bytes sem marcador (nenhum <c>FF</c>).</summary>
    private static byte[] Entropy(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)(index % 0xFE))];

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-03")]
    public void AJpegWithoutExtraneousBytesComesBackAsIsWithoutAllocatingACopy()
    {
        var jpeg = Jpeg(StartOfImage, App0, StartOfScan, Entropy(1024 * 1024), EndOfImage);
        // Aquecimento (JIT) fora da medida.
        JpegHeaderCleaner.WithoutExtraneousHeaderBytes(Jpeg(StartOfImage, App0, StartOfScan, Entropy(16), EndOfImage));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var cleaned = JpegHeaderCleaner.WithoutExtraneousHeaderBytes(jpeg);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        cleaned.ShouldBeSameAs(jpeg, "um JPEG sem bytes soltos tem de voltar como está");
        allocated.ShouldBeLessThan(
            64 * 1024,
            $"o limpador alocou {allocated} bytes para um JPEG de {jpeg.Length} bytes sem nenhum byte solto (a cópia só pode nascer no primeiro byte solto)");
    }

    public static TheoryData<string, byte[]> Malformed => new()
    {
        {
            // Byte solto, depois um APP1 que diz ter mais bytes do que o arquivo.
            "comprimento truncado",
            Jpeg(StartOfImage, [0x00], App0, [0xFF, 0xE1, 0x7F, 0xFF, 0x45, 0x78, 0x69, 0x66], StartOfScan, Entropy(32), EndOfImage)
        },
        {
            // Byte solto, depois FF 00 (preenchimento dos dados comprimidos) no meio do cabeçalho.
            "FF00 no cabeçalho",
            Jpeg(StartOfImage, [0x00], App0, [0xFF, 0x00, 0x00, 0x04, 0x01, 0x02], StartOfScan, Entropy(32), EndOfImage)
        },
        {
            // Byte solto e o arquivo acaba sem SOS (nem EOI).
            "sem SOS",
            Jpeg(StartOfImage, [0x00], App0, [0x00, 0x00])
        },
    };

    [Theory]
    [Trait("Spec", "SPEC-0024:H23-03")]
    [MemberData(nameof(Malformed))]
    public void AMalformedHeaderComesBackWithTheOriginalBytes(string @case, byte[] jpeg)
    {
        var cleaned = JpegHeaderCleaner.WithoutExtraneousHeaderBytes(jpeg);

        cleaned.ShouldBe(jpeg, $"{@case}: a estrutura não é reconhecida, então os bytes originais voltam (o decodificador decide)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-03")]
    public void AnExtraneousByteBetweenTwoHeaderSegmentsIsTheOnlyThingRemoved()
    {
        var entropy = Entropy(64);
        var jpeg = Jpeg(StartOfImage, App0, [0x00, 0x00], StartOfScan, entropy, EndOfImage);

        var cleaned = JpegHeaderCleaner.WithoutExtraneousHeaderBytes(jpeg);

        cleaned.ShouldBe(Jpeg(StartOfImage, App0, StartOfScan, entropy, EndOfImage));
    }
}
