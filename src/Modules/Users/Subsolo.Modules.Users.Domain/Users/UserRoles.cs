namespace Subsolo.Modules.Users.Domain.Users;

/// <summary>SPEC-0014 — papéis do usuário (os mesmos nomes das políticas de autorização do ADR-0003).</summary>
public static class UserRoles
{
    /// <summary>Fã: todo usuário com conta.</summary>
    public const string Fan = "fan";

    /// <summary>Artista: concedido pela spec de Perfis.</summary>
    public const string Artist = "artist";

    /// <summary>Administrador: e-mail em <c>Users:AdminEmails</c>.</summary>
    public const string Admin = "admin";
}
