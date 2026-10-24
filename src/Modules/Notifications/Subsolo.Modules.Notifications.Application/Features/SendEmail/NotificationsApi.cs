using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Diagnostics;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;
using Subsolo.Modules.Notifications.Application.Templates;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application.Features.SendEmail;

/// <summary>
/// SPEC-0018 — implementação de <see cref="INotificationsApi"/>: valida destinatário e dados, renderiza o modelo e
/// entrega pelo canal de e-mail (ADR-0014), dentro do span <c>notifications.email.send</c>. Entrada inválida volta
/// como falha sem chamar o canal; nada é lançado para o chamador além do cancelamento pedido por ele.
/// SPEC-0030 — os avisos (<see cref="EnqueueAsync"/>) e os dados da conta (<see cref="ForgetUserAsync"/>,
/// <see cref="ExportAsync"/>) usam o schema <c>notifications</c>: os serviços deles são pedidos ao contêiner só quando
/// chamados, para o <see cref="SendEmailAsync"/> continuar sem banco (hosts do e-mail transacional da SPEC-0018).
/// </summary>
internal sealed class NotificationsApi(
    IEnumerable<INotificationChannel> channels,
    IHostEnvironment environment,
    IServiceProvider services)
    : INotificationsApi
{
    private static readonly Error InvalidRecipient = Error.Validation(
        NotificationsErrors.InvalidRecipient,
        "O destinatário não é um e-mail válido.",
        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["to"] = ["E-mail inválido ou com mais de 254 caracteres."] });

    private static readonly Error InvalidData = Error.Validation(
        NotificationsErrors.InvalidData,
        "Os dados não servem para o modelo de e-mail.",
        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["data"] = ["Chave obrigatória ausente ou valor inválido."] });

    private readonly INotificationChannel _email = channels.Single(channel => channel.Channel == NotificationChannel.Email);

    public async Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = NotificationsTelemetry.StartSend(request.Template);

        var result = await SendAsync(request, cancellationToken);

        NotificationsTelemetry.Completed(
            activity, request.Template, result.IsSuccess ? NotificationsTelemetry.Sent : result.Error.Code);
        return result;
    }

    /// <inheritdoc />
    public Task<Result> EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct) =>
        services.GetRequiredService<NoticeQueue>().EnqueueAsync(userIds, notice, ct);

    /// <inheritdoc />
    public Task ForgetUserAsync(Guid userId, CancellationToken ct) =>
        services.GetRequiredService<INotificationsStore>().ForgetAsync(userId, ct);

    /// <inheritdoc />
    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct) =>
        await services.GetRequiredService<INotificationsStore>().ExportAsync(userId, ct);

    private async Task<Result> SendAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        if (!EmailRecipient.IsValid(request.To))
        {
            return Result.Failure(InvalidRecipient);
        }

        var content = request.Template switch
        {
            EmailTemplate.MagicLink => MagicLinkTemplate.Render(request.Data, allowHttp: environment.IsDevelopment()),
            _ => null,
        };
        if (content is null)
        {
            return Result.Failure(InvalidData);
        }

        return await _email.SendAsync(
            new EmailNotification(request.To, content.Subject, content.HtmlBody, content.TextBody), cancellationToken);
    }
}
