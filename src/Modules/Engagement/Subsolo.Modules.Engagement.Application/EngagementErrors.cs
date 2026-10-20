using Subsolo.Common.Domain;

namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0027 — códigos de erro do módulo Engagement (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class EngagementErrors
{
    /// <summary>404: cidade inexistente, ou perfil inexistente ou não aprovado.</summary>
    public const string TargetNotFound = "engagement.target_not_found";

    /// <summary>SPEC-0029 — 404: o show do "vou" não existe.</summary>
    public const string ShowNotFound = "engagement.show_not_found";

    /// <summary>SPEC-0029 — 409: o show foi cancelado, adiado ou já passou (não aceita "vou").</summary>
    public const string ShowNotAttendable = "engagement.show_not_attendable";

    /// <summary>SPEC-0029 — 400: consulta de contagens inválida (mais de 100 ids ou id inválido).</summary>
    public const string InvalidQuery = "engagement.invalid_query";

    internal static Error TargetNotFoundError() => Error.NotFound(TargetNotFound, "Não encontramos o que você quer seguir.");

    internal static Error ShowNotFoundError() => Error.NotFound(ShowNotFound, "Não encontramos esse show.");

    internal static Error ShowNotAttendableError() =>
        Error.Conflict(ShowNotAttendable, "Esse show foi cancelado, adiado ou já aconteceu: não dá para marcar \"vou\".");

    internal static Error InvalidQueryError(string field, string message) =>
        Error.Validation(
            InvalidQuery,
            "Consulta inválida.",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });
}
