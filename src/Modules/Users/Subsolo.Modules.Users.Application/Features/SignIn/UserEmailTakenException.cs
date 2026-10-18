namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>
/// SPEC-0016 (§7.6 b) — a gravação de um usuário novo bateu no índice único do e-mail: outra entrada simultânea com o
/// mesmo e-mail criou a conta antes. A unidade de trabalho descarta as mudanças pendentes antes de lançar, e o
/// <see cref="SignInService"/> entra na conta que já existe.
/// </summary>
public sealed class UserEmailTakenException : Exception
{
    public UserEmailTakenException()
        : base("Já existe um usuário com este e-mail.")
    {
    }

    public UserEmailTakenException(string message)
        : base(message)
    {
    }

    public UserEmailTakenException(Exception innerException)
        : base("Já existe um usuário com este e-mail.", innerException)
    {
    }

    public UserEmailTakenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
