namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — configuração do módulo Users (seção <see cref="SectionName"/>).</summary>
public sealed class UsersOptions
{
    public const string SectionName = "Users";

    /// <summary><c>Users:AdminEmails</c>: e-mails com o papel <c>admin</c> (não é segredo).</summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];

    /// <summary>SPEC-0020 — <c>Users:Google</c>: o login com Google (desligado sem configuração).</summary>
    public GoogleOptions Google { get; set; } = new();

    /// <summary>
    /// SPEC-0021 — <c>Users:MagicLinkEmailLimit</c>: links mágicos por e-mail a cada 15 minutos (padrão 3, SPEC-0016).
    /// Só a pilha local do E2E sobe o valor (as jornadas entram várias vezes com o mesmo admin).
    /// </summary>
    public int MagicLinkEmailLimit { get; set; } = DefaultMagicLinkEmailLimit;

    /// <summary>Limite padrão de links mágicos por e-mail na janela de 15 minutos.</summary>
    public const int DefaultMagicLinkEmailLimit = 3;
}
