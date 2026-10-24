using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DayBeforeReminder;

/// <summary>
/// SPEC-0032 — uma execução do lembrete na véspera (job <c>Engagement.DayBeforeReminder</c>), com a hora local a partir
/// da qual a véspera vale (<c>Engagement:ReminderHour</c>).
/// </summary>
internal sealed record SendDayBeforeRemindersCommand(int ReminderHour) : ICommand;

/// <summary>
/// SPEC-0032 — escolhe os lembretes de agora (<see cref="ReminderSelector"/>) e enfileira cada um no Notifications para
/// quem marcou "vou" no show. O Notifications só entrega pelos canais que o fã ligou; a chave
/// <c>reminder:{ShowId}:{início}</c> faz as execuções seguintes da véspera não mandarem outro.
/// </summary>
internal sealed partial class SendDayBeforeRemindersCommandHandler(
    ReminderSelector selector,
    INotificationsApi notifications,
    ILogger<SendDayBeforeRemindersCommandHandler> logger)
    : ICommandHandler<SendDayBeforeRemindersCommand>
{
    public async Task<Result> Handle(SendDayBeforeRemindersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var reminders = await selector.SelectAsync(command.ReminderHour, cancellationToken);
        var recipients = 0;
        foreach (var reminder in reminders)
        {
            var result = await notifications.EnqueueAsync(reminder.UserIds, reminder.Notice, cancellationToken);
            if (result.IsFailure)
            {
                LogRejected(logger, reminder.Notice.DedupeKey, result.Error.Code);
                continue;
            }

            recipients += reminder.UserIds.Count;
        }

        EngagementTelemetry.Reminders(recipients);
        LogCompleted(logger, reminders.Count, recipients);
        return Result.Success();
    }

    [LoggerMessage(
        EventName = "DayBeforeReminderCompleted",
        Level = LogLevel.Information,
        Message = "Lembrete na véspera: {Shows} show(s) amanhã com \"vou\", {Recipients} fã(s) considerado(s)")]
    private static partial void LogCompleted(ILogger logger, int shows, int recipients);

    [LoggerMessage(
        EventName = "DayBeforeReminderNoticeRejected",
        Level = LogLevel.Error,
        Message = "Lembrete na véspera: o Notifications recusou o aviso {DedupeKey} ({Code})")]
    private static partial void LogRejected(ILogger logger, string dedupeKey, string code);
}
