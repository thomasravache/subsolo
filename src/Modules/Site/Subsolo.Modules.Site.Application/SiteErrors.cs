namespace Subsolo.Modules.Site.Application;

/// <summary>SPEC-0028 — códigos de erro do módulo Site (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class SiteErrors
{
    /// <summary>400: nome fora de 2 a 60 caracteres ou link que não é <c>https</c>.</summary>
    public const string InvalidSupporter = "site.invalid_supporter";

    /// <summary>404: apoiador inexistente.</summary>
    public const string SupporterNotFound = "site.supporter_not_found";
}
