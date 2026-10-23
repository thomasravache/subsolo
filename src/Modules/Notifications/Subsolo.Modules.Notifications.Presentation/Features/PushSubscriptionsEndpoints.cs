using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.Application.Features.Subscriptions;

namespace Subsolo.Modules.Notifications.Presentation.Features;

/// <summary>
/// SPEC-0030 — <c>POST</c> e <c>DELETE /api/v1/me/push-subscriptions</c> (política <c>fan</c>, X-CSRF, rate limit
/// <c>default</c>): inscreve o aparelho (204, idempotente pelo endpoint; endpoint fora dos serviços de push conhecidos ou
/// sem chaves → 400 <c>notifications.invalid_subscription</c>, validado com o <see cref="EndpointValidation"/> comum,
/// H28-06) e remove a inscrição pelo endpoint (204).
/// </summary>
internal sealed class PushSubscriptionsEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/push-subscriptions";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(Route, SubscribeAsync)
            .RequireNoticesFan(mutation: true)
            .WithName("SubscribePush")
            .WithSummary("Ativar avisos neste aparelho")
            .WithDescription(
                "Grava a inscrição de push do navegador (endpoint https de um serviço de push conhecido e as chaves p256dh " +
                "e auth); a mesma inscrição de novo não duplica. A primeira inscrição liga o aviso de show novo. Exige o " +
                "cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, PushSubscriptionValidator.InvalidSubscription);

        app.MapDelete(Route, UnsubscribeAsync)
            .RequireNoticesFan(mutation: true)
            .WithName("UnsubscribePush")
            .WithSummary("Desativar avisos neste aparelho")
            .WithDescription("Remove a inscrição de push pelo endpoint (idempotente). Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> SubscribeAsync(
        PushSubscriptionRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<SubscribePushCommand> validator,
        [FromServices] ICommandHandler<SubscribePushCommand> handler,
        CancellationToken cancellationToken)
    {
        if (NotificationsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new SubscribePushCommand(userId, request.Endpoint, request.Keys?.P256dh, request.Keys?.Auth);
        if (await EndpointValidation.ValidateAsync(validator, command, "A inscrição de push é inválida.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return NotificationsEndpoints.NoContentOrProblem(await handler.Handle(command, cancellationToken));
    }

    private static async Task<IResult> UnsubscribeAsync(
        [FromBody] PushUnsubscribeRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UnsubscribePushCommand> handler,
        CancellationToken cancellationToken) =>
        NotificationsEndpoints.UserId(user) is { } userId
            ? NotificationsEndpoints.NoContentOrProblem(
                await handler.Handle(new UnsubscribePushCommand(userId, request.Endpoint), cancellationToken))
            : SecurityProblems.Unauthorized();
}

/// <summary>
/// SPEC-0030 — corpo de <c>POST /api/v1/me/push-subscriptions</c>: o <c>PushSubscription.toJSON()</c> do navegador. As
/// chaves ausentes chegam nulas mesmo com o tipo não anulável (o JSON não impõe a anotação) e viram
/// <c>notifications.invalid_subscription</c>.
/// </summary>
internal sealed record PushSubscriptionRequest(string? Endpoint, PushSubscriptionKeys Keys);

/// <summary>SPEC-0030 — as chaves da inscrição: a pública ECDH do navegador e o segredo de autenticação, em base64url.</summary>
internal sealed record PushSubscriptionKeys(string? P256dh, string? Auth);

/// <summary>SPEC-0030 — corpo de <c>DELETE /api/v1/me/push-subscriptions</c>: o endpoint da inscrição.</summary>
internal sealed record PushUnsubscribeRequest(string? Endpoint);
