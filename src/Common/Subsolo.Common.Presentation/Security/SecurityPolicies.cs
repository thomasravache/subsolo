namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0010 — nomes das políticas de autorização (ADR-0003) que os endpoints declaram em
/// <c>RequireAuthorization(&lt;política&gt;)</c>. Sem declaração, vale a política de fallback (usuário autenticado);
/// endpoint público declara <c>AllowAnonymous()</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Fã: papel padrão de todo usuário com conta.</summary>
    public const string Fan = "fan";

    /// <summary>Artista: perfil de banda, casa ou produtor aprovado e vinculado.</summary>
    public const string Artist = "artist";

    /// <summary>Administrador da plataforma.</summary>
    public const string Admin = "admin";
}

/// <summary>
/// SPEC-0010 — nomes das políticas de rate limiting (por IP), aplicadas com <c>RequireRateLimiting(&lt;política&gt;)</c>.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Login e envio de link mágico: 5 por minuto.</summary>
    public const string Auth = "auth";

    /// <summary>Denúncias: 10 por hora.</summary>
    public const string Report = "report";

    /// <summary>Demais endpoints com limite: 100 por minuto.</summary>
    public const string Default = "default";
}
