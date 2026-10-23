using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.Application.Features.Preferences;
using Subsolo.Modules.Notifications.Domain.Preferences;

namespace Subsolo.Modules.Notifications.Presentation.Features;

/// <summary>
/// SPEC-0030 — <c>GET</c> e <c>PUT /api/v1/me/notification-preferences</c> (política <c>fan</c>, rate limit
/// <c>default</c>, <c>no-store</c>; o PUT com X-CSRF): os quatro interruptores (padrões do §2: e-mail desligado, show novo
/// desligado até ativar o push) e quantos aparelhos estão inscritos.
/// </summary>
internal sealed class NotificationPreferencesEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/notification-preferences";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, GetAsync)
            .RequireNoticesFan(mutation: false)
            .WithName("GetNotificationPreferences")
            .WithSummary("Minhas preferências de aviso")
            .WithDescription(
                "Show novo por push, resumo diário por e-mail e lembrete na véspera por push e por e-mail, e quantos " +
                "aparelhos recebem os avisos.")
            .Produces<NotificationPreferencesResponse>(StatusCodes.Status200OK);

        app.MapPut(Route, UpdateAsync)
            .RequireNoticesFan(mutation: true)
            .WithName("UpdateNotificationPreferences")
            .WithSummary("Mudar preferências de aviso")
            .WithDescription("Grava os quatro interruptores e devolve as preferências. Exige o cabeçalho X-CSRF.")
            .Produces<NotificationPreferencesResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        [FromServices] IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesResponse> handler,
        CancellationToken cancellationToken)
    {
        if (NotificationsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new GetNotificationPreferencesQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }

    private static async Task<IResult> UpdateAsync(
        NotificationPreferencesRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesResponse> handler,
        CancellationToken cancellationToken)
    {
        if (NotificationsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var preferences = new NotificationPreferences(
            request.NewShowPush, request.DailyDigestEmail, request.ReminderPush, request.ReminderEmail);
        var result = await handler.Handle(new UpdateNotificationPreferencesCommand(userId, preferences), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0030 — corpo do <c>PUT /api/v1/me/notification-preferences</c>: os quatro interruptores.</summary>
internal sealed record NotificationPreferencesRequest(
    bool NewShowPush,
    bool DailyDigestEmail,
    bool ReminderPush,
    bool ReminderEmail);
