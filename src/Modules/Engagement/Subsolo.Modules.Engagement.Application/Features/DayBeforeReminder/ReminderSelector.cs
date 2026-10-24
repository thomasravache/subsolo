using System.Globalization;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DayBeforeReminder;

/// <summary>
/// SPEC-0032 — o seletor do lembrete na véspera (§6): dos shows <c>scheduled</c> que começam entre agora e agora + 48 h
/// (<see cref="IEventsCatalog"/>), os que começam "amanhã" no fuso da cidade, numa cidade em que já passou de
/// <c>reminderHour</c> hoje; para cada um com "vou" (<see cref="IAttendanceApi.GetAttendeesAsync"/>), o aviso
/// <c>reminder</c> com título "Amanhã: {Lineup}", corpo "{HH'h'mm} · {VenueName}, {CityName}", url
/// "/{CitySlug}/{Slug}" e chave "reminder:{ShowId}:{StartsAtUtc yyyyMMddHHmm}".
/// </summary>
internal sealed class ReminderSelector(IEventsCatalog catalog, IAttendanceApi attendances, TimeProvider clock)
{
    public const string Kind = "reminder";

    /// <summary>Os shows que começam neste intervalo a partir de agora são candidatos (o "amanhã" de qualquer fuso cabe).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(48);

    /// <summary>Os lembretes a enfileirar agora, um por show com "vou".</summary>
    public async Task<IReadOnlyList<ReminderNotice>> SelectAsync(int reminderHour, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var shows = await catalog.ListScheduledShowsStartingAsync(now, now + Window, cancellationToken);
        var notices = new List<ReminderNotice>();
        foreach (var show in shows)
        {
            if (!IsEve(show, now, reminderHour))
            {
                continue;
            }

            var attendees = await attendances.GetAttendeesAsync(show.ShowId, cancellationToken);
            if (attendees.Count == 0)
            {
                continue;
            }

            notices.Add(new ReminderNotice(attendees, NoticeOf(show)));
        }

        return notices;
    }

    /// <summary>No fuso da cidade, o show começa amanhã e já passou de <paramref name="reminderHour"/> hoje.</summary>
    private static bool IsEve(ShowSummary show, DateTimeOffset now, int reminderHour)
    {
        var today = NoticeText.Local(now, show.CityTimeZone);
        return today.Hour >= reminderHour
            && NoticeText.Local(show.StartsAtUtc, show.CityTimeZone).Date == today.Date.AddDays(1);
    }

    /// <summary>O aviso do §6: título, corpo, url e chave (o início em UTC na chave: um adiamento gera um lembrete novo).</summary>
    private static Notice NoticeOf(ShowSummary show)
    {
        var starts = NoticeText.Local(show.StartsAtUtc, show.CityTimeZone);
        return new Notice(
            Kind,
            string.Create(CultureInfo.InvariantCulture, $"reminder:{show.ShowId}:{show.StartsAtUtc.UtcDateTime:yyyyMMddHHmm}"),
            $"Amanhã: {NoticeText.Lineup(show.Lineup)}",
            string.Create(CultureInfo.InvariantCulture, $"{starts:HH}h{starts:mm} · {show.VenueName}, {show.CityName}"),
            $"/{show.CitySlug}/{show.Slug}");
    }
}

/// <summary>SPEC-0032 — o lembrete de um show para quem marcou "vou" nele.</summary>
internal sealed record ReminderNotice(IReadOnlyCollection<Guid> UserIds, Notice Notice);
