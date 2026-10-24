using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.Application.Features.Unsubscribe;

namespace Subsolo.Modules.Notifications.Presentation.Features;

/// <summary>
/// SPEC-0031 (H30-04, RFC 8058) — <c>POST /api/v1/notifications/unsubscribe/one-click?token=…</c> (público, rate limit
/// <c>default</c>, <c>no-store</c>): o provedor de e-mail chama a URL do cabeçalho <c>List-Unsubscribe</c> com o corpo
/// <c>application/x-www-form-urlencoded</c> <c>List-Unsubscribe=One-Click</c>, sem cookie nem <c>X-CSRF</c> — o token
/// assinado da query é a autorização (<see cref="CsrfExemptMetadata"/>). Desliga aquele tipo de e-mail (204); token
/// ausente, adulterado ou de outro formato → 400 <c>notifications.invalid_token</c>. Mesmo comando do
/// <see cref="UnsubscribeEndpoint"/> (a página <c>/avisos/descadastrar</c> não muda).
/// </summary>
internal sealed class OneClickUnsubscribeEndpoint : IEndpoint
{
    public const string Route = "/api/v1/notifications/unsubscribe/one-click";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireNoticesPublic()
            .RequireNoStore()
            .ExemptFromCsrf("RFC 8058: o provedor de e-mail faz o POST sem cookie; o token assinado da query autoriza")
            .WithName("UnsubscribeNoticeEmailOneClick")
            .WithSummary("Descadastrar e-mail de aviso em um clique")
            .WithDescription(
                "Descadastro em um clique (RFC 8058) do cabeçalho List-Unsubscribe dos e-mails de aviso: desliga o tipo " +
                "de e-mail do token (resumo diário ou lembrete na véspera), sem login nem X-CSRF. Corpo " +
                "List-Unsubscribe=One-Click.")
            .Accepts<OneClickUnsubscribeForm>("application/x-www-form-urlencoded")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UnsubscribeErrors.InvalidToken);

    private static async Task<IResult> HandleAsync(
        [FromQuery] string? token,
        [FromServices] ICommandHandler<UnsubscribeEmailCommand> handler,
        CancellationToken cancellationToken) =>
        NotificationsEndpoints.NoContentOrProblem(await handler.Handle(new UnsubscribeEmailCommand(token), cancellationToken));
}

/// <summary>
/// SPEC-0031 — corpo do <c>POST …/unsubscribe/one-click</c> (RFC 8058): <c>List-Unsubscribe=One-Click</c>. Só documenta o
/// formulário no OpenAPI; o token da query é o que importa.
/// </summary>
internal sealed record OneClickUnsubscribeForm(
    [property: System.Text.Json.Serialization.JsonPropertyName("List-Unsubscribe")] string ListUnsubscribe);
