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
}
