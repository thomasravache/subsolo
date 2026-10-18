using System.Security.Claims;
using Subsolo.Modules.Users.Application.Features.RequestMagicLink;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Features.SignInWithGoogle;

/// <summary>
/// SPEC-0020 — o retorno do Google já validado pelo handler OpenID Connect (PKCE, <c>state</c>, <c>nonce</c> e
/// <c>id_token</c>): com o e-mail verificado (claims <c>email</c> e <c>email_verified = true</c>, nomes do OIDC), entra
/// pelo <see cref="ISignInService"/> com o método Google e volta para o <c>returnTo</c> saneado (o mesmo saneador do
/// link mágico); sem e-mail ou sem <c>email_verified = true</c>, não entra e volta para
/// <see cref="EmailNotVerifiedRedirect"/>. SPEC-0021 (§7.6) — com o e-mail verificado e a entrada recusada pelo
/// <see cref="ISignInService"/>, volta para <see cref="ProviderErrorRedirect"/> (motivo <c>provider_error</c>).
/// </summary>
internal sealed class GoogleSignInCallback(ISignInService signIn)
{
    /// <summary>Destino de qualquer falha do provedor (cancelou, fora do ar, <c>state</c>/<c>nonce</c> inválidos).</summary>
    public const string ProviderErrorRedirect = "/entrar?erro=google";

    /// <summary>Destino quando o e-mail falta ou não é verificado.</summary>
    public const string EmailNotVerifiedRedirect = "/entrar?erro=google_email";

    /// <summary>Claim do e-mail (nome do OIDC).</summary>
    public const string EmailClaim = "email";

    /// <summary>Claim que diz se o provedor verificou o e-mail (nome do OIDC).</summary>
    public const string EmailVerifiedClaim = "email_verified";

    public async Task<GoogleCallbackOutcome> CompleteAsync(
        ClaimsPrincipal principal,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var email = principal.FindFirst(EmailClaim)?.Value;
        if (string.IsNullOrWhiteSpace(email) || !IsVerified(principal.FindFirst(EmailVerifiedClaim)?.Value))
        {
            return new GoogleCallbackOutcome(EmailNotVerifiedRedirect, SignedIn: false);
        }

        var result = await signIn.SignInAsync(email, SignInMethod.Google, cancellationToken);
        // SPEC-0021 (§7.6): com o e-mail verificado, a falha é da entrada (provider_error), não do e-mail.
        return result.IsSuccess
            ? new GoogleCallbackOutcome(ReturnToPath.Sanitize(returnTo), SignedIn: true)
            : new GoogleCallbackOutcome(ProviderErrorRedirect, SignedIn: false);
    }

    private static bool IsVerified(string? value) =>
        bool.TryParse(value, out var verified) && verified;
}

/// <summary>SPEC-0020 — para onde o navegador volta e se a sessão foi criada.</summary>
internal sealed record GoogleCallbackOutcome(string RedirectTo, bool SignedIn);
