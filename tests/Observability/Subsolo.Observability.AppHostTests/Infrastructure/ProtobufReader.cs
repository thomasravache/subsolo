namespace Subsolo.Observability.AppHostTests.Infrastructure;

/// <summary>
/// Leitor mínimo do formato binário do Protocol Buffers (varint, 64 bits, delimitado por tamanho, 32 bits): o
/// suficiente para o <see cref="OtlpTestCollector"/> ler as mensagens OTLP sem gerar classes a partir do .proto.
/// </summary>
internal sealed class ProtobufReader(ReadOnlyMemory<byte> buffer)
{
    private const int Varint = 0;
    private const int Fixed64 = 1;
    private const int LengthDelimited = 2;
    private const int Fixed32 = 5;

    private int _position;

    public int WireType { get; private set; }

    public bool TryReadField(out int field)
    {
        if (_position >= buffer.Length)
        {
            field = 0;
            return false;
        }

        var tag = ReadVarint();
        field = (int)(tag >> 3);
        WireType = (int)(tag & 0x7);
        return true;
    }

    public ulong ReadVarint()
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            var b = buffer.Span[_position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }
        }

        throw new FormatException("varint inválido na mensagem OTLP");
    }

    public ReadOnlyMemory<byte> ReadBytes()
    {
        var length = (int)ReadVarint();
        var slice = buffer.Slice(_position, length);
        _position += length;
        return slice;
    }

    public string ReadString() => System.Text.Encoding.UTF8.GetString(ReadBytes().Span);

    public double ReadDouble()
    {
        var value = BitConverter.ToDouble(buffer.Span.Slice(_position, 8));
        _position += 8;
        return value;
    }

    public void Skip()
    {
        switch (WireType)
        {
            case Varint:
                ReadVarint();
                break;
            case Fixed64:
                _position += 8;
                break;
            case LengthDelimited:
                ReadBytes();
                break;
            case Fixed32:
                _position += 4;
                break;
            default:
                throw new FormatException($"tipo de campo {WireType} não suportado na mensagem OTLP");
        }
    }
}
