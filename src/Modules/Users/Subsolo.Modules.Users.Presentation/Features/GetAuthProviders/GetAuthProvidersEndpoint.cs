using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application;

namespace Subsolo.Modules.Users.Presentation.Features.GetAuthProviders;

/// <summary>
/// SPEC-0020 — <c>GET /api/v1/auth/providers</c> (público, rate limit <c>default</c>): os provedores de login ligados
/// (<c>{ google }</c>; o Google só com <c>Users:Google:Authority</c> e <c>ClientId</c>), para a tela de entrar mostrar
/// ou esconder o botão.
/// </summary>
internal sealed class GetAuthProvidersEndpoint : IEndpoint
{
    public const string Route = "/api/v1/auth/providers";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, Handle)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("GetAuthProviders")
            .WithTags("Conta")
            .WithSummary("Provedores de login")
            .WithDescription("Quais formas de entrar, além do link por e-mail, estão disponíveis.")
            .Produces<AuthProvidersResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static Ok<AuthProvidersResponse> Handle(IOptions<UsersOptions> options) =>
        TypedResults.Ok(new AuthProvidersResponse(options.Value.Google.IsEnabled));
}

/// <summary>SPEC-0020 — provedores de login ligados: <c>google</c> = "Continuar com Google" disponível.</summary>
internal sealed record AuthProvidersResponse(bool Google);
