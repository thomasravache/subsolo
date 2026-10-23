using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.Domain.Preferences;

namespace Subsolo.Modules.Notifications.Application.Features.Preferences;

/// <summary>
/// SPEC-0030 — corpo de <c>GET</c>/<c>PUT /api/v1/me/notification-preferences</c> (§6): os quatro interruptores e
/// quantos aparelhos do fã estão inscritos para o push.
/// </summary>
public sealed record NotificationPreferencesResponse(
    bool NewShowPush,
    bool DailyDigestEmail,
    bool ReminderPush,
    bool ReminderEmail,
    int PushDevices)
{
    internal static NotificationPreferencesResponse From(NotificationPreferences preferences, int pushDevices) =>
        new(preferences.NewShowPush, preferences.DailyDigestEmail, preferences.ReminderPush, preferences.ReminderEmail, pushDevices);
}

/// <summary>SPEC-0030 — as preferências do fã (as gravadas ou os padrões do §2).</summary>
internal sealed record GetNotificationPreferencesQuery(Guid UserId) : IQuery<NotificationPreferencesResponse>;

internal sealed class GetNotificationPreferencesQueryHandler(INotificationsStore store)
    : IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesResponse>
{
    public async Task<Result<NotificationPreferencesResponse>> Handle(
        GetNotificationPreferencesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (preferences, devices) = await store.PreferencesAsync(query.UserId, cancellationToken);
        return Result.Success(NotificationPreferencesResponse.From(preferences, devices));
    }
}

/// <summary>SPEC-0030 — grava os quatro interruptores do fã e devolve o corpo do <c>GET</c>.</summary>
internal sealed record UpdateNotificationPreferencesCommand(Guid UserId, NotificationPreferences Preferences)
    : ICommand<NotificationPreferencesResponse>;

internal sealed class UpdateNotificationPreferencesCommandHandler(INotificationsStore store)
    : ICommandHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesResponse>
{
    public async Task<Result<NotificationPreferencesResponse>> Handle(
        UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await store.SavePreferencesAsync(command.UserId, command.Preferences, cancellationToken);
        var (preferences, devices) = await store.PreferencesAsync(command.UserId, cancellationToken);
        return Result.Success(NotificationPreferencesResponse.From(preferences, devices));
    }
}
