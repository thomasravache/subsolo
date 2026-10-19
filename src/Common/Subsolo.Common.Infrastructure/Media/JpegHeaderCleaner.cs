using System.Buffers.Binary;

namespace Subsolo.Common.Infrastructure.Media;

/// <summary>
/// SPEC-0023 (H22-04) — bytes soltos entre os segmentos do cabeçalho de um JPEG (antes de um marcador, até o início dos
/// dados comprimidos) fazem o decodificador só avisar ("extraneous bytes before marker") e ele os pula; com a
/// decodificação estrita (todo aviso é erro, para dado corrompido e arquivo truncado continuarem 422), esse aviso
/// benigno barraria um flyer íntegro. O limpador tira esses bytes antes de decodificar, do mesmo jeito que o
/// decodificador faria, e não mexe em mais nada: os dados comprimidos (onde mora a corrupção de verdade) passam intactos.
/// </summary>
internal static class JpegHeaderCleaner
{
    private const byte Prefix = 0xFF;

    private const byte StartOfImage = 0xD8;

    private const byte EndOfImage = 0xD9;

    private const byte StartOfScan = 0xDA;

    /// <summary>
    /// <paramref name="jpeg"/> sem os bytes soltos do cabeçalho; o próprio <paramref name="jpeg"/> se não houver nenhum
    /// ou se a estrutura não for reconhecida (o decodificador decide). SPEC-0024 (H23-03): a cópia só é alocada no
    /// primeiro byte solto, e comprimento truncado, <c>FF 00</c> no cabeçalho ou a falta do SOS devolvem os bytes
    /// originais.
    /// </summary>
    public static byte[] WithoutExtraneousHeaderBytes(byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        if (jpeg.Length < 4 || jpeg[0] != Prefix || jpeg[1] != StartOfImage)
        {
            return jpeg;
        }

        // Nasce no primeiro byte solto, com tudo o que veio antes dele.
        byte[]? output = null;
        var written = 0;
        var index = 2;
        while (index < jpeg.Length)
        {
            if (jpeg[index] != Prefix)
            {
                // Byte solto antes do próximo marcador: o decodificador pularia com um aviso.
                if (output is null)
                {
                    output = new byte[jpeg.Length];
                    Array.Copy(jpeg, output, index);
                    written = index;
                }

                index++;
                continue;
            }

            if (index + 1 >= jpeg.Length)
            {
                return jpeg;
            }

            var marker = jpeg[index + 1];
            int size;
            if (marker == Prefix)
            {
                // Byte de preenchimento antes do marcador.
                size = 1;
            }
            else if (marker is StartOfScan or EndOfImage)
            {
                if (output is null)
                {
                    return jpeg;
                }

                Array.Copy(jpeg, index, output, written, jpeg.Length - index);
                written += jpeg.Length - index;
                return output.AsSpan(0, written).ToArray();
            }
            else if (marker == 0x00)
            {
                // FF 00 só existe dentro dos dados comprimidos: no cabeçalho, a estrutura não é reconhecida.
                return jpeg;
            }
            else if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                size = 2;
            }
            else
            {
                if (index + 3 >= jpeg.Length)
                {
                    return jpeg;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(index + 2));
                if (length < 2 || index + 2 + length > jpeg.Length)
                {
                    return jpeg;
                }

                size = 2 + length;
            }

            if (output is not null)
            {
                Array.Copy(jpeg, index, output, written, size);
                written += size;
            }

            index += size;
        }

        // Sem SOS (nem EOI): a estrutura não é reconhecida.
        return jpeg;
    }
}
