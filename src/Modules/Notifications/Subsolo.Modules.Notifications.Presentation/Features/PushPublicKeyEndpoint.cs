using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;

namespace Subsolo.Modules.Notifications.Presentation.Features;

/// <summary>
/// SPEC-0030 — <c>GET /api/v1/push/public-key</c> (público, rate limit <c>default</c>): a chave pública VAPID que o
/// navegador passa ao <c>pushManager.subscribe</c>; com o canal desligado (sem chaves fora de Development), 503.
/// </summary>
internal sealed class PushPublicKeyEndpoint : IEndpoint
{
    public const string Route = "/api/v1/push/public-key";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, Handle)
            .RequireNoticesPublic()
            .WithName("GetPushPublicKey")
            .WithSummary("Chave pública dos avisos")
            .WithDescription("A chave pública VAPID (P-256, base64url) para inscrever o navegador nos avisos por push.")
            .Produces<PushPublicKeyResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static IResult Handle([FromServices] IWebPushKeys keys) =>
        keys.PublicKey is { } publicKey ? Results.Ok(new PushPublicKeyResponse(publicKey)) : SecurityProblems.Unavailable();
}

/// <summary>SPEC-0030 — corpo de <c>GET /api/v1/push/public-key</c>.</summary>
internal sealed record PushPublicKeyResponse(string PublicKey);
