namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — configuração do módulo Users (seção <see cref="SectionName"/>).</summary>
public sealed class UsersOptions
{
    public const string SectionName = "Users";

    /// <summary><c>Users:AdminEmails</c>: e-mails com o papel <c>admin</c> (não é segredo).</summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];

    /// <summary>SPEC-0020 — <c>Users:Google</c>: o login com Google (desligado sem configuração).</summary>
    public GoogleOptions Google { get; set; } = new();
}
