using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — limites e erros do processamento de imagem. Os códigos são do bloco comum (<c>media.*</c>);
/// cada módulo os traduz para os seus (o <c>Events</c>, para <c>events.flyer_*</c>).
/// </summary>
public static class MediaErrors
{
    /// <summary>Tamanho máximo do arquivo original: 10 MB (10 × 1024 × 1024 bytes).</summary>
    public const long MaxBytes = 10L * 1024 * 1024;

    /// <summary>Número máximo de pixels do original (40 megapixels), contra "bomba de descompressão".</summary>
    public const long MaxPixels = 40_000_000;

    /// <summary>400: o conteúdo não é JPEG, PNG nem WebP.</summary>
    public const string InvalidType = "media.invalid_type";

    /// <summary>400: acima de <see cref="MaxBytes"/>.</summary>
    public const string TooLarge = "media.too_large";

    /// <summary>400: acima de <see cref="MaxPixels"/>.</summary>
    public const string TooManyPixels = "media.too_many_pixels";

    /// <summary>422: cabeçalho legível, mas a imagem não pôde ser decodificada.</summary>
    public const string Unprocessable = "media.unprocessable";

    /// <summary>
    /// SPEC-0023 (H22-05) — 503: a fila de processamento (um por vez) não liberou a vez dentro do tempo limite; quem
    /// recebe responde com o código <c>service.unavailable</c>.
    /// </summary>
    public const string Busy = "media.busy";

    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>(StringComparer.Ordinal);

    public static Error InvalidTypeError() =>
        Error.Validation(InvalidType, "O arquivo não é uma imagem JPEG, PNG ou WebP.", NoFields);

    public static Error TooLargeError() =>
        Error.Validation(TooLarge, "O arquivo passa de 10 MB.", NoFields);

    public static Error TooManyPixelsError() =>
        Error.Validation(TooManyPixels, "A imagem passa de 40 megapixels.", NoFields);

    public static Error BusyError() =>
        Error.Failure(Busy, "O processamento de imagens está ocupado. Tente de novo em instantes.");

    public static Error UnprocessableError() =>
        Error.Unprocessable(Unprocessable, "A imagem não pôde ser processada (arquivo corrompido?).");
}
