using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Domain.Notices;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application.Features.Enqueue;

/// <summary>
/// SPEC-0030 — <see cref="INotificationsApi.EnqueueAsync"/>: valida o aviso, lê as preferências e os aparelhos de todos
/// os destinatários de uma vez, pede ao <see cref="IRecipientDirectory"/> (Users) só os e-mails de quem tem o e-mail
/// daquele tipo ligado, planeja as entregas (<see cref="DeliveryPlanner"/>) e grava as pendentes em lote; o envio sai
/// depois, pelo job de entregas do módulo, com novas tentativas. Idempotente por (usuário, chave, canal).
/// </summary>
internal sealed partial class NoticeQueue(
    INotificationsStore store,
    IRecipientDirectory directory,
    IHostEnvironment environment,
    TimeProvider clock,
    ILogger<NoticeQueue> logger)
{
    public const int MaxDedupeKeyLength = 200;
    public const int MaxTitleLength = 120;
    public const int MaxBodyLength = 1000;
    public const int MaxUrlLength = 2048;

    private static readonly Error InvalidNotice = Error.Validation(
        NotificationsErrors.InvalidData,
        "O aviso é inválido.",
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["notice"] = ["Tipo desconhecido, chave, título ou corpo vazios ou longos demais, ou URL inválida."],
        });

    public async Task<Result> EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(notice);
        if (!IsValid(notice))
        {
            return Result.Failure(InvalidNotice);
        }

        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Result.Success();
        }

        var watch = Stopwatch.StartNew();
        var states = await store.RecipientsAsync(ids, ct);
        var wantEmail = states.Where(state => state.Preferences.AllowsEmail(notice.Kind)).Select(state => state.UserId).ToList();
        IReadOnlyDictionary<Guid, string> emails = wantEmail.Count > 0
            ? await directory.GetEmailsAsync(wantEmail, ct)
            : new Dictionary<Guid, string>();
        var recipients = states
            .Select(state => new RecipientChannels(
                state.UserId, state.Preferences, state.HasPushSubscription, emails.ContainsKey(state.UserId)))
            .ToList();
        var registered = await store.RegisteredAsync(notice.DedupeKey, ids, ct);

        var planned = DeliveryPlanner.Plan(notice, recipients, registered);
        if (planned.Count > 0)
        {
            await store.AddDeliveriesAsync(notice, planned, clock.GetUtcNow(), ct);
        }

        LogEnqueued(logger, notice.Kind, ids.Count, planned.Count, watch.ElapsedMilliseconds);
        return Result.Success();
    }

    private bool IsValid(Notice notice) =>
        NoticeKinds.IsKnown(notice.Kind)
        && notice.DedupeKey is { Length: > 0 and <= MaxDedupeKeyLength }
        && !string.IsNullOrWhiteSpace(notice.Title) && notice.Title.Length <= MaxTitleLength
        && !string.IsNullOrWhiteSpace(notice.Body) && notice.Body.Length <= MaxBodyLength
        && notice.Url is { Length: > 0 and <= MaxUrlLength }
        && Uri.TryCreate(notice.Url, UriKind.Absolute, out var url)
        && !string.IsNullOrEmpty(url.Host)
        && (url.Scheme == Uri.UriSchemeHttps || (environment.IsDevelopment() && url.Scheme == Uri.UriSchemeHttp));

    [LoggerMessage(
        EventName = "NoticeEnqueued",
        Level = LogLevel.Information,
        Message = "Aviso {Kind} para {Recipients} destinatário(s): {Deliveries} entrega(s) nova(s) em {ElapsedMs} ms")]
    private static partial void LogEnqueued(ILogger logger, string kind, int recipients, int deliveries, long elapsedMs);
}
