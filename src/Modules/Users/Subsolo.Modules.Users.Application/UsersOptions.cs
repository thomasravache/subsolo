namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — configuração do módulo Users (seção <see cref="SectionName"/>).</summary>
public sealed class UsersOptions
{
    public const string SectionName = "Users";

    /// <summary><c>Users:AdminEmails</c>: e-mails com o papel <c>admin</c> (não é segredo).</summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];
}
