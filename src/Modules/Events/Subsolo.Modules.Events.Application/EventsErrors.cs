using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application;

/// <summary>SPEC-0017 — códigos de erro do cadastro de cidades e estilos (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class EventsErrors
{
    /// <summary>400: nome, UF, fuso ou slug da cidade inválido.</summary>
    public const string InvalidCity = "events.invalid_city";

    /// <summary>400: o slug da cidade é um endereço reservado.</summary>
    public const string CitySlugReserved = "events.city_slug_reserved";

    /// <summary>409: já existe cidade com o slug.</summary>
    public const string CitySlugTaken = "events.city_slug_taken";

    /// <summary>404: nenhuma cidade com o slug.</summary>
    public const string CityNotFound = "events.city_not_found";

    /// <summary>400: nome do estilo inválido.</summary>
    public const string InvalidStyle = "events.invalid_style";

    /// <summary>409: já existe estilo com o slug (ou com o nome).</summary>
    public const string StyleTaken = "events.style_taken";

    /// <summary>404: nenhum estilo com o slug.</summary>
    public const string StyleNotFound = "events.style_not_found";

    /// <summary>SPEC-0022 — 400: o conteúdo do arquivo não é JPEG, PNG nem WebP (a extensão não conta).</summary>
    public const string FlyerInvalidType = "events.flyer_invalid_type";

    /// <summary>SPEC-0022 — 400: o arquivo passa de 10 MB (10 × 1024 × 1024 bytes).</summary>
    public const string FlyerTooLarge = "events.flyer_too_large";

    /// <summary>SPEC-0022 — 400: a imagem passa de 40 megapixels.</summary>
    public const string FlyerTooManyPixels = "events.flyer_too_many_pixels";

    /// <summary>SPEC-0022 — 400: o envio não tem a parte <c>file</c> (ou ela está vazia).</summary>
    public const string FlyerMissing = "events.flyer_missing";

    /// <summary>SPEC-0022 — 422: cabeçalho de imagem válido, mas a imagem não pôde ser processada.</summary>
    public const string FlyerUnprocessable = "events.flyer_unprocessable";

    /// <summary>SPEC-0023 — 400: um campo do show é inválido (o campo vai em <c>errors</c>).</summary>
    public const string InvalidShow = "events.invalid_show";

    /// <summary>SPEC-0023 — 400: o <c>flyerId</c> não é de um flyer enviado.</summary>
    public const string FlyerNotFound = "events.flyer_not_found";

    /// <summary>SPEC-0023 — 409: já existe show com o mesmo título na mesma data e cidade (mesmo slug).</summary>
    public const string ShowSlugTaken = "events.show_slug_taken";

    /// <summary>SPEC-0023 — 404: nenhum show com o id.</summary>
    public const string ShowNotFound = "events.show_not_found";

    /// <summary>SPEC-0023 — 400: situação fora de <c>scheduled</c>, <c>cancelled</c> e <c>postponed</c>.</summary>
    public const string InvalidStatus = "events.invalid_status";

    /// <summary>SPEC-0023 — 400: <c>period</c> fora de <c>upcoming</c> e <c>past</c> (em <c>GET /me/shows</c>).</summary>
    public const string InvalidPeriod = "events.invalid_period";

    /// <summary>Mensagem do campo para um slug gerado fora do formato.</summary>
    internal const string GeneratedSlugMessage = "O nome precisa ter ao menos duas letras ou dígitos para gerar o endereço.";

    internal static Error FieldError(string code, string description, string field, string message) =>
        Error.Validation(
            code,
            description,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    internal static Error CityNotFoundError(string slug) =>
        Error.NotFound(CityNotFound, $"Nenhuma cidade com o endereço \"{slug}\".");

    internal static Error StyleNotFoundError(string slug) =>
        Error.NotFound(StyleNotFound, $"Nenhum estilo com o slug \"{slug}\".");

    internal static Error CitySlugTakenError() => Error.Conflict(CitySlugTaken, "Esse endereço já existe.");

    internal static Error StyleTakenError() => Error.Conflict(StyleTaken, "Esse estilo já existe.");

    /// <summary>SPEC-0022 — 400 <see cref="FlyerMissing"/>, no campo <c>file</c>.</summary>
    public static Error FlyerMissingError() =>
        FieldError(FlyerMissing, "Envie o flyer no campo file.", "file", "Arquivo ausente ou vazio.");

    internal static Error FlyerInvalidTypeError() =>
        FieldError(FlyerInvalidType, "O flyer precisa ser JPEG, PNG ou WebP.", "file", "Tipo de arquivo não aceito.");

    /// <summary>SPEC-0022 — 400 <see cref="FlyerTooLarge"/>, no campo <c>file</c>.</summary>
    public static Error FlyerTooLargeError() =>
        FieldError(FlyerTooLarge, "O flyer passa de 10 MB.", "file", "Arquivo acima de 10 MB.");

    internal static Error FlyerTooManyPixelsError() =>
        FieldError(FlyerTooManyPixels, "O flyer passa de 40 megapixels.", "file", "Imagem acima de 40 megapixels.");

    /// <summary>SPEC-0023 — 400 <see cref="InvalidShow"/> no campo <paramref name="field"/>.</summary>
    internal static Error InvalidShowError(string field, string message) =>
        FieldError(InvalidShow, "Dados do show inválidos.", field, message);

    internal static Error FlyerNotFoundError() =>
        FieldError(FlyerNotFound, "Flyer não encontrado.", "FlyerId", "Envie o flyer de novo.");

    internal static Error ShowSlugTakenError() =>
        Error.Conflict(ShowSlugTaken, "Já existe um show com esse nome nessa data nessa cidade.");

    /// <summary>SPEC-0023 — 403 <c>auth.forbidden</c> (o mesmo código da política): o usuário não é dono do organizador.</summary>
    internal static Error ShowForbiddenError() =>
        Error.Forbidden("auth.forbidden", "Só o dono do perfil organizador (ou o admin) mexe neste show.");

    internal static Error InvalidPeriodError() =>
        FieldError(InvalidPeriod, "Período inválido.", "period", "Use upcoming ou past.");

    internal static Error ShowNotFoundError() => Error.NotFound(ShowNotFound, "Show não encontrado.");

    internal static Error InvalidStatusError() =>
        FieldError(InvalidStatus, "Situação do show inválida.", "Status", "Use scheduled, cancelled ou postponed.");

    internal static Error FlyerUnprocessableError() =>
        Error.Unprocessable(FlyerUnprocessable, "O flyer não pôde ser processado (arquivo corrompido?).");
}
