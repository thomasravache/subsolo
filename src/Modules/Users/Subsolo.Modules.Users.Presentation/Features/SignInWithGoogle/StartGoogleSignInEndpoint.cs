using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.RequestMagicLink;
using Subsolo.Modules.Users.Application.Features.SignInWithGoogle;

namespace Subsolo.Modules.Users.Presentation.Features.SignInWithGoogle;

/// <summary>
/// SPEC-0020 — <c>GET /api/v1/auth/google?returnTo=</c> (público, rate limit <c>auth</c>, <c>Cache-Control:
/// no-store</c>): inicia o login com Google — 302 para o authorize do provedor (código + PKCE + <c>state</c> +
/// <c>nonce</c>), com o <c>returnTo</c> saneado guardado no <c>state</c> protegido. Google não configurado → 404
/// <c>users.provider_disabled</c>; provedor fora do ar (descoberta inacessível) → 302 para <c>/entrar?erro=google</c>.
/// </summary>
internal sealed class StartGoogleSignInEndpoint : IEndpoint
{
    public const string Route = "/api/v1/auth/google";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .RequireNoStore()
            .WithName("StartGoogleSignIn")
            .WithTags("Conta")
            .WithSummary("Entrar com Google")
            .WithDescription(
                "Redireciona para o login do Google e, na volta, entra no Subsolo e segue para o returnTo (só caminho " +
                "relativo do próprio site; qualquer outro vira /). Falhas voltam para /entrar?erro=google ou " +
                "/entrar?erro=google_email.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblemCode(StatusCodes.Status404NotFound, UsersErrors.ProviderDisabled)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? returnTo,
        HttpContext httpContext,
        IOptions<UsersOptions> options)
    {
        if (!options.Value.Google.IsEnabled)
        {
            return ApiResults.Problem(Result.Failure(UsersErrors.ProviderDisabledError()));
        }

        var properties = new AuthenticationProperties { RedirectUri = ReturnToPath.Default };
        properties.Items[GoogleSignIn.ReturnToItem] = ReturnToPath.Sanitize(returnTo);
        try
        {
            await httpContext.ChallengeAsync(GoogleSignIn.Scheme, properties);
            return Results.Empty;
        }
        catch (Exception exception) when (IsProviderUnavailable(exception, httpContext))
        {
            GoogleSignIn.LogProviderUnavailable(httpContext, exception);
            return Results.Redirect(GoogleSignInCallback.ProviderErrorRedirect);
        }
    }

    /// <summary>A descoberta do provedor falhou (fora do ar, tempo esgotado), e não o cancelamento da requisição.</summary>
    private static bool IsProviderUnavailable(Exception exception, HttpContext httpContext) =>
        !httpContext.RequestAborted.IsCancellationRequested
        && !httpContext.Response.HasStarted
        && exception is InvalidOperationException or HttpRequestException or TaskCanceledException;
}
