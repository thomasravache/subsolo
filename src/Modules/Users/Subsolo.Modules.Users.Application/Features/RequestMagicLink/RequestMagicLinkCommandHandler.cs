using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — valida e normaliza o e-mail, aplica o limite por e-mail (3 links a cada 15 min; acima dele, sucesso
/// sem envio), grava o link (só o hash do token) e manda o e-mail <see cref="EmailTemplate.MagicLink"/> pela
/// <see cref="INotificationsApi"/> com <c>link = {App:PublicUrl}/entrar/confirmar?token=…</c> e <c>minutes = 15</c>.
/// </summary>
/// <remarks>Os logs não levam o e-mail nem o token (ADR-0008).</remarks>
internal sealed partial class RequestMagicLinkCommandHandler(
    IMagicLinkTokenRepository tokens,
    IUsersUnitOfWork unitOfWork,
    INotificationsApi notifications,
    IOptions<PublicUrlOptions> publicUrl,
    TimeProvider clock,
    ILogger<RequestMagicLinkCommandHandler> logger)
    : ICommandHandler<RequestMagicLinkCommand>
{
    /// <summary>Links por e-mail dentro de <see cref="EmailWindow"/>.</summary>
    public const int EmailLimit = 3;

    /// <summary>Janela do limite por e-mail.</summary>
    public static readonly TimeSpan EmailWindow = TimeSpan.FromMinutes(15);

    /// <summary>Caminho da página que consome o link (o token vai na query <c>token</c>).</summary>
    public const string ConfirmPath = "/entrar/confirmar";

    public async Task<Result> Handle(RequestMagicLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (EmailAddress.Normalize(command.Email) is not { } email)
        {
            return Result.Failure(UsersErrors.InvalidEmailError());
        }

        var now = clock.GetUtcNow();
        if (await tokens.CountIssuedSinceAsync(email, now - EmailWindow, cancellationToken) >= EmailLimit)
        {
            // Acima do limite por e-mail a resposta é a mesma (não revela nada), só sem envio.
            UsersTelemetry.MagicLinkRequested(UsersTelemetry.RequestLimited);
            LogEmailLimited(logger, EmailLimit, EmailWindow.TotalMinutes);
            return Result.Success();
        }

        var (token, value) = MagicLinkToken.Issue(email, ReturnToPath.Sanitize(command.ReturnTo), now);
        tokens.Add(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var sent = await notifications.SendEmailAsync(
            new EmailRequest(
                email,
                EmailTemplate.MagicLink,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["link"] = Link(value),
                    ["minutes"] = MagicLinkToken.Lifetime.TotalMinutes.ToString(CultureInfo.InvariantCulture),
                }),
            cancellationToken);
        if (sent.IsFailure)
        {
            UsersTelemetry.MagicLinkRequested(UsersTelemetry.RequestFailed);
            LogSendFailed(logger, sent.Error.Code);
            return Result.Failure(sent.Error);
        }

        UsersTelemetry.MagicLinkRequested(UsersTelemetry.RequestSent);
        return Result.Success();
    }

    /// <summary><c>{App:PublicUrl}/entrar/confirmar?token=…</c> (token em base64url, já seguro na URL).</summary>
    private string Link(string token) =>
        $"{publicUrl.Value.PublicUrl.TrimEnd('/')}{ConfirmPath}?token={Uri.EscapeDataString(token)}";

    [LoggerMessage(Level = LogLevel.Information, Message = "Link mágico não enviado: limite de {Limit} links em {WindowMinutes} min por e-mail")]
    private static partial void LogEmailLimited(ILogger logger, int limit, double windowMinutes);

    // Plano de Rollout: EventName base do alerta de falhas de envio do link no Seq (mais de 5 em 10 min).
    [LoggerMessage(
        EventName = "MagicLinkSendFailed",
        Level = LogLevel.Warning,
        Message = "Link mágico não enviado: o Notifications respondeu {ErrorCode}")]
    private static partial void LogSendFailed(ILogger logger, string errorCode);
}
