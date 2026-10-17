using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — códigos de erro do módulo Users (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class UsersErrors
{
    /// <summary>E-mail fora do formato ou com mais de 254 caracteres.</summary>
    public const string InvalidEmail = "users.invalid_email";

    /// <summary>Usuário da sessão não encontrado.</summary>
    public const string UserNotFound = "users.not_found";

    internal static Error InvalidEmailError() =>
        Error.Validation(
            InvalidEmail,
            "O e-mail é inválido.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["email"] = ["Informe um e-mail válido com até 254 caracteres."],
            });

    internal static Error UserNotFoundError() =>
        Error.NotFound(UserNotFound, "Usuário não encontrado.");
}
