namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — códigos de erro do módulo Users (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class UsersErrors
{
    /// <summary>E-mail fora do formato ou com mais de 254 caracteres.</summary>
    public const string InvalidEmail = "users.invalid_email";
}
