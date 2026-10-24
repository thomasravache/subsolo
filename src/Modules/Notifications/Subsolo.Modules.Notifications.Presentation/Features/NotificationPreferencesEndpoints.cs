using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Notifications.Application.Features.Preferences;
using Subsolo.Modules.Notifications.Domain.Preferences;

namespace Subsolo.Modules.Notifications.Presentation.Features;

/// <summary>
/// SPEC-0030 — <c>GET</c> e <c>PUT /api/v1/me/notification-preferences</c> (política <c>fan</c>, rate limit
/// <c>default</c>, <c>no-store</c>; o PUT com X-CSRF): os quatro interruptores (padrões do §2: e-mail desligado, show novo
/// desligado até ativar o push) e quantos aparelhos estão inscritos. SPEC-0031 (H30-03): o PUT exige os quatro campos
/// booleanos; um ausente ou de outro tipo (texto, número, nulo) → 400 <c>notifications.invalid_preferences</c>, sem
/// mudar nada. O corpo é lido aqui (e não pela ligação de parâmetros do ASP.NET, que responderia um 400 sem código).
/// </summary>
internal sealed class NotificationPreferencesEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/notification-preferences";

    /// <summary>SPEC-0031 — tamanho máximo do corpo do PUT (os quatro booleanos cabem em bem menos).</summary>
    private const int MaxBodyBytes = 4 * 1024;

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
            .WithDescription(
                "Grava os quatro interruptores (todos obrigatórios, booleanos) e devolve as preferências. Exige o " +
                "cabeçalho X-CSRF.")
            .Accepts<NotificationPreferencesRequest>("application/json")
            .Produces<NotificationPreferencesResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, PreferencesErrors.InvalidPreferences);
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
        HttpRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesResponse> handler,
        CancellationToken cancellationToken)
    {
        if (NotificationsEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        if (await ReadAsync(request, cancellationToken) is not { } preferences)
        {
            return ApiResults.Problem(Result.Failure(PreferencesErrors.Invalid));
        }

        var result = await handler.Handle(new UpdateNotificationPreferencesCommand(userId, preferences), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }

    /// <summary>
    /// O corpo JSON com os quatro campos booleanos (nomes em camelCase, como o resto da API), ou <see langword="null"/>
    /// com algum ausente, de outro tipo, JSON inválido ou corpo grande demais.
    /// </summary>
    private static async Task<NotificationPreferences?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType() || request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxBodyBytes;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            var body = document.RootElement;
            return body.ValueKind == JsonValueKind.Object
                && Flag(body, "newShowPush") is { } newShowPush
                && Flag(body, "dailyDigestEmail") is { } dailyDigestEmail
                && Flag(body, "reminderPush") is { } reminderPush
                && Flag(body, "reminderEmail") is { } reminderEmail
                ? new NotificationPreferences(newShowPush, dailyDigestEmail, reminderPush, reminderEmail)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null;
        }
    }

    private static bool? Flag(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}

/// <summary>SPEC-0031 (H30-03) — erro do PUT de preferências sem os quatro campos booleanos.</summary>
internal static class PreferencesErrors
{
    public const string InvalidPreferences = "notifications.invalid_preferences";

    public static readonly Error Invalid = Error.Validation(
        InvalidPreferences,
        "As preferências de aviso são inválidas.",
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["body"] = ["Informe newShowPush, dailyDigestEmail, reminderPush e reminderEmail, todos booleanos."],
        });
}

/// <summary>
/// SPEC-0030 — corpo do <c>PUT /api/v1/me/notification-preferences</c>: os quatro interruptores (SPEC-0031, H30-03:
/// todos obrigatórios). Só documenta o corpo no OpenAPI; a leitura é a do endpoint.
/// </summary>
internal sealed record NotificationPreferencesRequest(
    bool NewShowPush,
    bool DailyDigestEmail,
    bool ReminderPush,
    bool ReminderEmail);
