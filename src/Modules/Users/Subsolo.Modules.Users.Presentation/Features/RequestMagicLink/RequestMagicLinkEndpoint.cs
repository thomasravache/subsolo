using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.RequestMagicLink;

namespace Subsolo.Modules.Users.Presentation.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — <c>POST /api/v1/auth/magic-link</c> (público, X-CSRF pelo filtro do host, rate limit <c>auth</c>,
/// <c>Cache-Control: no-store</c>): pede o link mágico. A resposta é sempre 202 <c>{}</c>, exista a conta ou não (e
/// também acima do limite por e-mail, sem envio); SMTP fora do ar → 503 <c>notifications.email_unavailable</c>.
/// </summary>
internal sealed class RequestMagicLinkEndpoint : IEndpoint
{
    public const string Route = "/api/v1/auth/magic-link";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .RequireNoStore()
            .WithName("RequestMagicLink")
            .WithTags("Conta")
            .WithSummary("Pedir link de acesso")
            .WithDescription(
                "Manda por e-mail um link de acesso de uso único, válido por 15 minutos. A resposta é a mesma exista a " +
                "conta ou não. Exige o cabeçalho X-CSRF.")
            .Produces<RequestMagicLinkResponse>(StatusCodes.Status202Accepted)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UsersErrors.InvalidEmail)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, NotificationsErrors.EmailUnavailable);

    private static async Task<IResult> HandleAsync(
        RequestMagicLinkRequest request,
        [FromServices] ICommandHandler<RequestMagicLinkCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RequestMagicLinkCommand(request.Email, request.ReturnTo), cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Json(new RequestMagicLinkResponse(), statusCode: StatusCodes.Status202Accepted);
        }

        return result.Error.Code == NotificationsErrors.EmailUnavailable
            ? EmailUnavailable()
            : ApiResults.Problem(result);
    }

    /// <summary>503 no formato de <see cref="ApiResults"/> (código em <c>title</c> e em <c>extensions.code</c>).</summary>
    private static IResult EmailUnavailable() =>
        Results.Problem(
            detail: "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: NotificationsErrors.EmailUnavailable,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ApiResults.CodeExtension] = NotificationsErrors.EmailUnavailable,
            });
}

/// <summary>SPEC-0016 — corpo do pedido de link: e-mail (até 254 caracteres) e o caminho de volta opcional.</summary>
internal sealed record RequestMagicLinkRequest(
    [property: MaxLength(254)] string Email,
    string? ReturnTo);

/// <summary>SPEC-0016 — corpo do 202 do pedido de link: vazio (<c>{}</c>), igual para qualquer e-mail.</summary>
internal sealed record RequestMagicLinkResponse;
