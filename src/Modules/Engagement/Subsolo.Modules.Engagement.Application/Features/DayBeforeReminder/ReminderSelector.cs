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
    /// <summary>Os lembretes a enfileirar agora, um por show com "vou".</summary>
    public Task<IReadOnlyList<ReminderNotice>> SelectAsync(int reminderHour, CancellationToken cancellationToken)
    {
        // Esqueleto do Test-writer (SPEC-0032): o comportamento entra na fase Green.
        _ = (catalog, attendances, clock, reminderHour, cancellationToken);
        throw new NotImplementedException("SPEC-0032: seletor do lembrete na véspera");
    }
}

/// <summary>SPEC-0032 — o lembrete de um show para quem marcou "vou" nele.</summary>
internal sealed record ReminderNotice(IReadOnlyCollection<Guid> UserIds, Notice Notice);
