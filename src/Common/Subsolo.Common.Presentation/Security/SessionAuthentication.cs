using Microsoft.AspNetCore.Authentication.Cookies;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0014 — o esquema de autenticação do cookie de sessão <c>__Host-subsolo</c> e as claims que ele carrega: só o
/// id do usuário e o token da sessão (256 bits em base64url; o banco guarda só o hash). Os papéis não ficam no cookie:
/// a cada requisição o host valida a sessão (<c>ISessionValidator</c>) e põe os papéis atuais no principal.
/// </summary>
public static class SessionAuthentication
{
    /// <summary>Esquema de autenticação do cookie de sessão.</summary>
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>Claim com o id do usuário.</summary>
    public const string UserIdClaim = System.Security.Claims.ClaimTypes.NameIdentifier;

    /// <summary>Claim com o token da sessão.</summary>
    public const string SessionTokenClaim = "subsolo:session";
}
