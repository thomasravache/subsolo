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
/// SPEC-0030 — <c>POST /api/v1/notifications/unsubscribe</c> (público, X-CSRF pelo filtro do host, rate limit
/// <c>default</c>, <c>no-store</c>): o link de descadastro de um e-mail de aviso desliga aquele tipo de e-mail (204);
/// token ausente, adulterado ou de outro formato → 400 <c>notifications.invalid_token</c>.
/// </summary>
internal sealed class UnsubscribeEndpoint : IEndpoint
{
    public const string Route = "/api/v1/notifications/unsubscribe";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireNoticesPublic()
            .RequireNoStore()
            .WithName("UnsubscribeNoticeEmail")
            .WithSummary("Descadastrar e-mail de aviso")
            .WithDescription(
                "Desliga o tipo de e-mail de aviso (resumo diário ou lembrete na véspera) do token do link de " +
                "descadastro, sem login. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, UnsubscribeErrors.InvalidToken)
            .WithMetadata(new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid));

    private static async Task<IResult> HandleAsync(
        UnsubscribeRequest request,
        [FromServices] ICommandHandler<UnsubscribeEmailCommand> handler,
        CancellationToken cancellationToken) =>
        NotificationsEndpoints.NoContentOrProblem(await handler.Handle(new UnsubscribeEmailCommand(request.Token), cancellationToken));
}

/// <summary>SPEC-0030 — corpo de <c>POST /api/v1/notifications/unsubscribe</c>: o token do link.</summary>
internal sealed record UnsubscribeRequest(string? Token);
