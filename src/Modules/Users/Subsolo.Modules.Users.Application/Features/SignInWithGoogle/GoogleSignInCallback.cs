using System.Security.Claims;
using Subsolo.Modules.Users.Application.Features.SignIn;

namespace Subsolo.Modules.Users.Application.Features.SignInWithGoogle;

/// <summary>
/// SPEC-0020 — o retorno do Google já validado pelo handler OpenID Connect (PKCE, <c>state</c>, <c>nonce</c> e
/// <c>id_token</c>): com o e-mail verificado (claims <c>email</c> e <c>email_verified = true</c>, nomes do OIDC), entra
/// pelo <see cref="ISignInService"/> com o método Google e volta para o <c>returnTo</c> saneado (o mesmo saneador do
/// link mágico); sem e-mail ou sem <c>email_verified = true</c>, não entra e volta para
/// <see cref="EmailNotVerifiedRedirect"/>.
/// </summary>
internal sealed class GoogleSignInCallback(ISignInService signIn)
{
    /// <summary>Destino de qualquer falha do provedor (cancelou, fora do ar, <c>state</c>/<c>nonce</c> inválidos).</summary>
    public const string ProviderErrorRedirect = "/entrar?erro=google";

    /// <summary>Destino quando o e-mail falta ou não é verificado.</summary>
    public const string EmailNotVerifiedRedirect = "/entrar?erro=google_email";

    public Task<GoogleCallbackOutcome> CompleteAsync(
        ClaimsPrincipal principal,
        string? returnTo,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException($"SPEC-0020: implementar o retorno do Google ({signIn.GetType().Name}).");
}

/// <summary>SPEC-0020 — para onde o navegador volta e se a sessão foi criada.</summary>
internal sealed record GoogleCallbackOutcome(string RedirectTo, bool SignedIn);
